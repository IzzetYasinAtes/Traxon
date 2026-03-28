using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Traxon.CryptoTrader.Application.Abstractions;
using Traxon.CryptoTrader.Application.Polymarket.Models;
using Traxon.CryptoTrader.Domain.Common;
using Traxon.CryptoTrader.Domain.Market;
using Traxon.CryptoTrader.Domain.Trading;
using Traxon.CryptoTrader.Polymarket.Options;

namespace Traxon.CryptoTrader.Polymarket.Engines;

public sealed class PolymarketEngine : ITradingEngine, IAsyncDisposable
{
    private readonly IPolymarketClient          _client;
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

    private readonly Portfolio _portfolio;

    public string EngineName => "LivePoly";

    public PolymarketEngine(
        IPolymarketClient client,
        IMarketDiscoveryService discovery,
        IOptions<PolymarketOptions> options,
        ITradeLogger tradeLogger,
        ILogger<PolymarketEngine> logger)
    {
        _client      = client;
        _discovery   = discovery;
        _options     = options.Value;
        _tradeLogger = tradeLogger;
        _logger      = logger;
        _portfolio   = new Portfolio(EngineName, InitialBalance);
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
            var order = new PolymarketOrderRequest
            {
                TokenId   = market.RelevantTokenId,
                Price     = limitPrice,
                Size      = positionSize,
                Side      = "BUY",
                OrderType = "GTC"
            };

            var placeResult = await _client.PlaceOrderAsync(order, ct);
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
                var cancelResult = await _client.CancelOrderAsync(orderId, ct);
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
    /// Polymarket'te resolution otomatik olur; bu method şimdilik no-op.
    /// WebSocket event'leri üzerinden otomatik çözüm gelecek versiyonda eklenecek.
    /// </summary>
    public Task CheckPositionsAsync(Candle candle, CancellationToken ct = default)
        => Task.CompletedTask;

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
