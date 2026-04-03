using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Traxon.CryptoTrader.Application.Abstractions;
using Traxon.CryptoTrader.Domain.Common;
using Traxon.CryptoTrader.Domain.Market;
using Traxon.CryptoTrader.Domain.Trading;
using Traxon.CryptoTrader.Polymarket.Options;

namespace Traxon.CryptoTrader.Polymarket.Engines;

public sealed class PolymarketEngine : ITradingEngine, IAsyncDisposable
{
    private readonly IPolymarketClient          _client;
    private readonly IPolymarketSigningClient   _signingClient;
    private readonly IMarketDiscoveryService    _discovery;
    private readonly PolymarketOptions          _options;
    private readonly ITradeLogger               _tradeLogger;
    private readonly ILogger<PolymarketEngine>  _logger;

    private readonly ConcurrentDictionary<Guid, Trade>  _openTrades    = new();
    private readonly ConcurrentDictionary<Guid, string> _tradeToOrderId = new();
    private readonly SemaphoreSlim                       _lock          = new(1, 1);

    private PeriodicTimer?          _heartbeatTimer;
    private CancellationTokenSource? _heartbeatCts;
    private Task?                    _heartbeatTask;

    private const decimal InitialBalance = 10_000m;
    private const decimal TakerFeeRate  = 0.018m; // %1.8 taker fee

    private readonly Portfolio _portfolio;

    public string EngineName => "LivePoly";

    public PolymarketEngine(
        IPolymarketClient client,
        IPolymarketSigningClient signingClient,
        IMarketDiscoveryService discovery,
        IOptions<PolymarketOptions> options,
        ITradeLogger tradeLogger,
        ILogger<PolymarketEngine> logger)
    {
        _client        = client;
        _signingClient = signingClient;
        _discovery     = discovery;
        _options       = options.Value;
        _tradeLogger   = tradeLogger;
        _logger        = logger;
        _portfolio     = new Portfolio(EngineName, InitialBalance);
    }

    public Task<Result<bool>> IsReadyAsync(CancellationToken ct = default)
    {
        if (!_options.Enabled)
            return Task.FromResult(Result<bool>.Failure(Error.Disabled));

        return Task.FromResult(Result<bool>.Success(true));
    }

    public async Task<Result<Trade>> OpenPositionAsync(
        Signal signal, CancellationToken ct = default)
    {
        if (!_options.Enabled)
            return Result<Trade>.Failure(Error.Disabled);

        await _lock.WaitAsync(ct);
        try
        {
            if (_openTrades.Values.Any(t => t.Asset == signal.Asset))
                return Result<Trade>.Failure(Error.DuplicatePosition);

            // Find matching market
            var discoverResult = await _discovery.DiscoverMarketsAsync(ct);
            if (discoverResult.IsFailure)
                return Result<Trade>.Failure(discoverResult.Error!);

            var targetDirection = signal.Direction == SignalDirection.Up ? "Up" : "Down";
            var market = discoverResult.Value!
                .FirstOrDefault(m => m.UnderlyingAsset.Equals(signal.Asset.Symbol.Replace("USDT", ""),
                                         StringComparison.OrdinalIgnoreCase)
                                  && m.Direction == targetDirection);

            if (market is null)
            {
                _logger.LogWarning("No Polymarket market found for {Asset} {Direction}",
                    signal.Asset.Symbol, signal.Direction);
                return Result<Trade>.Failure(Error.MarketNotFound);
            }

            // Get current midpoint price
            var midpointResult = await _client.GetMidpointAsync(market.RelevantTokenId, ct);
            if (midpointResult.IsFailure)
                return Result<Trade>.Failure(midpointResult.Error!);

            var marketPrice  = midpointResult.Value;
            var positionSize = Math.Min(
                signal.KellyFraction * _portfolio.Balance,
                _options.MaxPositionSizeUsdc);

            // Place maker limit order slightly below midpoint
            var limitPrice = Math.Max(0.01m, marketPrice - 0.01m);

            var placeResult = await _signingClient.CreateAndPostOrderAsync(
                market.RelevantTokenId, limitPrice, positionSize, "BUY", "GTC", ct);
            if (placeResult.IsFailure)
                return Result<Trade>.Failure(placeResult.Error!);

            var orderId = placeResult.Value!;

            var indicatorJson = JsonSerializer.Serialize(new
            {
                rsi       = signal.Indicators.Rsi.Value,
                macd_hist = signal.Indicators.Macd.Histogram,
                atr       = signal.Indicators.Atr.Value,
                regime    = signal.Regime.ToString(),
                bullish   = signal.Indicators.BullishCount()
            });

            var trade = new Trade(
                engine:            EngineName,
                asset:             signal.Asset,
                timeFrame:         signal.TimeFrame,
                direction:         signal.Direction,
                entryPrice:        limitPrice,
                fairValue:         signal.FairValue,
                edge:              signal.Edge,
                positionSize:      positionSize,
                kellyFraction:     signal.KellyFraction,
                muEstimate:        signal.MuEstimate,
                sigmaEstimate:     signal.SigmaEstimate,
                regime:            signal.Regime,
                indicatorSnapshot: indicatorJson,
                entryReason:       $"FV:{signal.FairValue:F3} Edge:{signal.Edge:F3} OrderId:{orderId}");

            _openTrades[trade.Id]    = trade;
            _tradeToOrderId[trade.Id] = orderId;

            await _tradeLogger.LogTradeOpenedAsync(trade, ct);

            _logger.LogInformation(
                "[LivePoly] Trade OPENED: {Symbol} {Direction} Entry:{Entry:F4} Size:{Size:F2} Order:{OrderId}",
                trade.Asset.Symbol, trade.Direction, trade.EntryPrice, trade.PositionSize, orderId);

            EnsureHeartbeatStarted();

            return Result<Trade>.Success(trade);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<Result<Trade>> ClosePositionAsync(
        Guid tradeId, string reason, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (!_openTrades.TryRemove(tradeId, out var trade))
                return Result<Trade>.Failure(Error.TradeNotFound);

            _tradeToOrderId.TryRemove(tradeId, out var orderId);

            if (!string.IsNullOrEmpty(orderId))
            {
                var cancelResult = await _signingClient.CancelOrderAsync(orderId, ct);
                if (cancelResult.IsFailure)
                    _logger.LogWarning("Failed to cancel order {OrderId}: {Error}",
                        orderId, cancelResult.Error?.Message);
            }

            trade.Close(0m, TradeOutcome.Loss, -trade.PositionSize);
            await _tradeLogger.LogTradeClosedAsync(trade, ct);
            await _tradeLogger.LogPortfolioSnapshotAsync(_portfolio, ct);

            _logger.LogInformation(
                "[LivePoly] Trade CLOSED: {Symbol} {Direction} Reason:{Reason}",
                trade.Asset.Symbol, trade.Direction, reason);

            return Result<Trade>.Success(trade);
        }
        finally
        {
            _lock.Release();
        }
    }

    public Task<Result<IReadOnlyList<Trade>>> GetOpenTradesAsync(CancellationToken ct = default)
        => Task.FromResult(Result<IReadOnlyList<Trade>>.Success(
               _openTrades.Values.ToList().AsReadOnly()));

    public Task<Result<Portfolio>> GetPortfolioAsync(CancellationToken ct = default)
        => Task.FromResult(Result<Portfolio>.Success(_portfolio));

    /// <summary>
    /// Açık pozisyonları kontrol eder. Polymarket market'i resolve olduysa
    /// (fiyat >= 0.95 veya <= 0.05) trade'i kapatır.
    /// </summary>
    public async Task CheckPositionsAsync(Candle candle, CancellationToken ct = default)
    {
        if (_openTrades.IsEmpty) return;

        var tradesToClose = new List<(Guid tradeId, decimal exitPrice, TradeOutcome outcome, decimal pnl)>();

        foreach (var (tradeId, trade) in _openTrades)
        {
            try
            {
                var discoverResult = await _discovery.DiscoverMarketsAsync(ct);
                if (discoverResult.IsFailure) continue;

                var targetDirection = trade.Direction == SignalDirection.Up ? "Up" : "Down";
                var market = discoverResult.Value!
                    .FirstOrDefault(m => m.UnderlyingAsset.Equals(
                        trade.Asset.Symbol.Replace("USDT", ""),
                        StringComparison.OrdinalIgnoreCase)
                        && m.Direction == targetDirection);

                if (market is null) continue;

                var midpointResult = await _client.GetMidpointAsync(market.RelevantTokenId, ct);
                if (midpointResult.IsFailure) continue;

                var currentPrice = midpointResult.Value;

                if (currentPrice >= 0.95m)
                {
                    // YES kazandi — tam resolve
                    var exitPrice = 1.0m;
                    var fee = trade.PositionSize * TakerFeeRate;
                    var pnl = (exitPrice - trade.EntryPrice) / trade.EntryPrice * trade.PositionSize - fee;
                    tradesToClose.Add((tradeId, exitPrice, TradeOutcome.Win, pnl));
                }
                else if (currentPrice <= 0.05m)
                {
                    // NO kazandi — tam kayip
                    var exitPrice = 0.0m;
                    var pnl = -trade.PositionSize;
                    tradesToClose.Add((tradeId, exitPrice, TradeOutcome.Loss, pnl));
                }
                // Henuz resolve olmadiysa bekle
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[LivePoly] CheckPositions error for trade {TradeId}", tradeId);
            }
        }

        foreach (var (tradeId, exitPrice, outcome, pnl) in tradesToClose)
        {
            if (_openTrades.TryRemove(tradeId, out var trade))
            {
                _tradeToOrderId.TryRemove(tradeId, out _);

                trade.Close(exitPrice, outcome, pnl);

                await _tradeLogger.LogTradeClosedAsync(trade, ct);
                await _tradeLogger.LogPortfolioSnapshotAsync(_portfolio, ct);

                _logger.LogInformation(
                    "[LivePoly] Trade RESOLVED: {Symbol} {Direction} Exit:{Exit:F2} PnL:{PnL:F4} Outcome:{Outcome}",
                    trade.Asset.Symbol, trade.Direction, exitPrice, pnl, outcome);
            }
        }
    }

    private void EnsureHeartbeatStarted()
    {
        if (_heartbeatTask is not null)
            return;

        _heartbeatCts  = new CancellationTokenSource();
        _heartbeatTimer = new PeriodicTimer(
            TimeSpan.FromSeconds(_options.HeartbeatIntervalSeconds));
        _heartbeatTask = RunHeartbeatLoopAsync(_heartbeatCts.Token);
    }

    private async Task RunHeartbeatLoopAsync(CancellationToken ct)
    {
        try
        {
            while (await _heartbeatTimer!.WaitForNextTickAsync(ct))
            {
                var result = await _client.SendHeartbeatAsync(ct);
                if (result.IsFailure)
                    _logger.LogWarning("Polymarket heartbeat failed: {Error}", result.Error?.Message);
                else
                    _logger.LogDebug("Polymarket heartbeat sent");

                // Canli bakiye senkronizasyonu (signing client uzerinden)
                var balanceResult = await _signingClient.GetBalanceAsync(ct);
                if (balanceResult.IsSuccess)
                    _logger.LogDebug("[LivePoly] Polymarket balance: ${Balance:F2}", balanceResult.Value);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_heartbeatCts is not null)
        {
            await _heartbeatCts.CancelAsync();
            _heartbeatCts.Dispose();
        }

        _heartbeatTimer?.Dispose();

        if (_heartbeatTask is not null)
        {
            try { await _heartbeatTask.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }

        _lock.Dispose();
    }
}
