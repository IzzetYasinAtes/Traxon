using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Traxon.CryptoTrader.Application.Abstractions;
using Traxon.CryptoTrader.Domain.Assets;
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

    private readonly ConcurrentDictionary<Guid, Trade>     _openTrades         = new();
    private readonly ConcurrentDictionary<Guid, string>    _tradeToOrderId    = new();
    private readonly ConcurrentDictionary<Guid, string>    _tradeToTokenId    = new();
    private readonly ConcurrentDictionary<Guid, Guid>      _tradeToPositionMap = new();
    private readonly SemaphoreSlim                       _lock              = new(1, 1);
    private readonly SemaphoreSlim                       _initLock          = new(1, 1);
    private volatile bool                                _initialized;

    private PeriodicTimer?          _heartbeatTimer;
    private CancellationTokenSource? _heartbeatCts;
    private Task?                    _heartbeatTask;

    private const decimal InitialBalance = 10_000m;
    private const decimal FeeRate        = 0.072m; // Polymarket crypto taker fee rate

    // Polymarket sadece bu asset'ler için market açar
    private static readonly HashSet<string> SupportedAssets = new(StringComparer.OrdinalIgnoreCase)
    {
        "BTC", "ETH", "SOL", "XRP", "DOGE", "BNB", "HYPE"
    };

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

    /// <summary>
    /// Worker restart sonrasi in-memory portfolio ve _openTrades'i DB'den restore eder (bir kez).
    /// </summary>
    private async Task EnsureInitializedAsync(CancellationToken ct)
    {
        if (_initialized) return;
        await _initLock.WaitAsync(ct);
        try
        {
            if (_initialized) return;

            var snapshot = await _tradeLogger.GetLatestSnapshotAsync(EngineName, ct);
            if (snapshot is not null)
            {
                var winCount = snapshot.WinRate.HasValue && snapshot.TradeCount > 0
                    ? (int)Math.Round(snapshot.WinRate.Value * snapshot.TradeCount)
                    : 0;
                var lossCount = snapshot.TradeCount - winCount;
                // Use DB-calculated realized PnL instead of snapshot.TotalPnL to prevent drift.
                var realPnL = await _tradeLogger.GetRealizedPnLAsync(EngineName, ct);
                var unencumberedBalance = InitialBalance + realPnL;
                _portfolio.Restore(unencumberedBalance, realPnL, winCount, lossCount);
                _logger.LogInformation(
                    "[LivePoly] Portfolio restored: Balance:{Balance:F2} (unencumbered) PnL:{PnL:F2} (snapshot had:{SnapshotPnL:F2}) W:{Win} L:{Loss}",
                    unencumberedBalance, realPnL, snapshot.TotalPnL, winCount, lossCount);
            }

            var openTrades = await _tradeLogger.GetOpenTradesAsync(EngineName, ct);
            foreach (var trade in openTrades ?? [])
            {
                _openTrades[trade.Id] = trade;

                // Restore token ID from EntryReason ("...OrderId:XXX" or "...Token:XXX")
                var reason = trade.EntryReason ?? "";
                var tokenPrefix = "Token:";
                var tokenIdx = reason.IndexOf(tokenPrefix, StringComparison.Ordinal);
                if (tokenIdx >= 0)
                {
                    var tokenId = reason[(tokenIdx + tokenPrefix.Length)..].Trim();
                    if (!string.IsNullOrEmpty(tokenId))
                        _tradeToTokenId[trade.Id] = tokenId;
                }

                var position = new Position(
                    asset:        trade.Asset,
                    timeFrame:    trade.TimeFrame,
                    direction:    trade.Direction,
                    entryPrice:   trade.EntryPrice,
                    positionSize: trade.PositionSize,
                    stopLoss:     null,
                    takeProfit:   null);
                _portfolio.OpenPosition(position);
                _tradeToPositionMap[trade.Id] = position.Id;
            }

            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
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
        await EnsureInitializedAsync(ct);

        // Always sync portfolio from DB before opening — prevents ghost position / PnL drift
        await SyncPortfolioFromDbAsync(ct);

        if (!_options.Enabled)
            return Result<Trade>.Failure(Error.Disabled);

        if (_portfolio.Balance <= 0)
            return Result<Trade>.Failure(new Error("LivePoly.InsufficientBalance", "Balance is zero"));

        await _lock.WaitAsync(ct);
        try
        {
            // Polymarket 5-min window boundary: epoch % 300 == 0
            // +3s buffer handles T=0 entry where signal fires at :59.9 (candle boundary)
            var unixNow = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 3;
            var currentWindowStart = DateTimeOffset.FromUnixTimeSeconds(unixNow - (unixNow % 300)).UtcDateTime;
            if (_openTrades.Values.Any(t => t.Asset == signal.Asset && t.OpenedAt >= currentWindowStart))
                return Result<Trade>.Failure(Error.DuplicatePosition);

            // Fast pre-check: Polymarket sadece belirli asset'ler için market açar
            var baseAsset = signal.Asset.Symbol.Replace("USDT", "");
            if (!SupportedAssets.Contains(baseAsset))
                return Result<Trade>.Failure(Error.MarketNotFound);

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

            var positionSize = Math.Max(_portfolio.Balance * 0.02m, 1m);

            // FAK market order (taker) — USDC miktarı gönder, mevcut fiyattan anında al
            var entryPrice = marketPrice;

            var placeResult = await _signingClient.CreateAndPostMarketOrderAsync(
                market.RelevantTokenId, positionSize, "BUY", "FAK", ct);
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
                entryPrice:        entryPrice,
                fairValue:         signal.FairValue,
                edge:              signal.Edge,
                positionSize:      positionSize,
                kellyFraction:     signal.KellyFraction,
                muEstimate:        signal.MuEstimate,
                sigmaEstimate:     signal.SigmaEstimate,
                regime:            signal.Regime,
                indicatorSnapshot: indicatorJson,
                entryReason:       $"FV:{signal.FairValue:F3} Edge:{signal.Edge:F3} OrderId:{orderId}");

            var position = new Position(
                asset:        signal.Asset,
                timeFrame:    signal.TimeFrame,
                direction:    signal.Direction,
                entryPrice:   entryPrice,
                positionSize: positionSize,
                stopLoss:     null,
                takeProfit:   null);

            var openResult = _portfolio.OpenPosition(position);
            if (openResult.IsFailure)
                return Result<Trade>.Failure(openResult.Error!);

            _openTrades[trade.Id]           = trade;
            _tradeToOrderId[trade.Id]       = orderId;
            _tradeToTokenId[trade.Id]       = market.RelevantTokenId;
            _tradeToPositionMap[trade.Id]   = position.Id;

            await _tradeLogger.LogTradeOpenedAsync(trade, ct);
            await _tradeLogger.LogPortfolioSnapshotAsync(_portfolio, ct);

            _logger.LogInformation(
                "[LivePoly] Trade OPENED: {Symbol} {Direction} Entry:{Entry:F4} Size:{Size:F2} Order:{OrderId}",
                trade.Asset.Symbol, trade.Direction, trade.EntryPrice, trade.PositionSize, orderId);

            // Heartbeat sadece gerçek trade açıkken başlar
            if (_portfolio.Balance > 0)
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
            _tradeToTokenId.TryRemove(tradeId, out _);
            _tradeToPositionMap.TryRemove(tradeId, out var posId);

            if (!string.IsNullOrEmpty(orderId))
            {
                var cancelResult = await _signingClient.CancelOrderAsync(orderId, ct);
                if (cancelResult.IsFailure)
                    _logger.LogWarning("Failed to cancel order {OrderId}: {Error}",
                        orderId, cancelResult.Error?.Message);
            }

            trade.Close(0m, TradeOutcome.Loss, -trade.PositionSize);
            _portfolio.ClosePosition(posId, -trade.PositionSize, TradeOutcome.Loss);

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
    /// Açık pozisyonları kontrol eder. Polymarket market'i resolve olduysa trade'i kapatır.
    /// Gamma API'den ResolvedPrice gelene kadar bekler — Chainlink oracle resolution.
    /// </summary>
    public async Task CheckPositionsAsync(Candle candle, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);

        if (_openTrades.IsEmpty) return;

        var tradesToClose = new List<(Guid tradeId, decimal exitPrice, TradeOutcome outcome, decimal pnl)>();

        // Gamma API'den tüm marketleri al (closed dahil)
        var discoverResult = await _discovery.DiscoverAllMarketsAsync(ct);
        if (discoverResult.IsFailure)
        {
            _logger.LogWarning("[LivePoly] DiscoverAllMarkets failed: {Error}", discoverResult.Error?.Message);
            return;
        }

        var allMarkets = discoverResult.Value!;

        foreach (var (tradeId, trade) in _openTrades)
        {
            try
            {
                _tradeToTokenId.TryGetValue(tradeId, out var tokenId);
                if (string.IsNullOrEmpty(tokenId)) continue;

                var market = allMarkets.FirstOrDefault(m => m.RelevantTokenId == tokenId);
                if (market is null || !market.ResolvedPrice.HasValue) continue;

                var resolvedPrice = market.ResolvedPrice.Value;
                var tradeShares = trade.PositionSize / trade.EntryPrice;
                var tradeFee = tradeShares * FeeRate * trade.EntryPrice * (1m - trade.EntryPrice);

                if (resolvedPrice >= 0.99m)
                {
                    var pnl = (tradeShares * 1.0m) - trade.PositionSize - tradeFee;
                    tradesToClose.Add((tradeId, resolvedPrice, TradeOutcome.Win, pnl));
                }
                else
                {
                    var pnl = -(trade.PositionSize + tradeFee);
                    tradesToClose.Add((tradeId, resolvedPrice, TradeOutcome.Loss, pnl));
                }

                _logger.LogInformation(
                    "[LivePoly] Market RESOLVED via GammaAPI for {Asset} {Direction}: ResolvedPrice={Price}",
                    trade.Asset.Symbol, trade.Direction, resolvedPrice);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[LivePoly] CheckPositions error for trade {TradeId}", tradeId);
            }
        }

        // ── Close resolved trades ──
        foreach (var (tradeId, exitPrice, outcome, pnl) in tradesToClose)
        {
            if (_openTrades.TryRemove(tradeId, out var trade))
            {
                _tradeToOrderId.TryRemove(tradeId, out _);
                _tradeToTokenId.TryRemove(tradeId, out _);
                _tradeToPositionMap.TryRemove(tradeId, out var posId);

                trade.Close(exitPrice, outcome, pnl);
                _portfolio.ClosePosition(posId, pnl, outcome);

                await _tradeLogger.LogTradeClosedAsync(trade, ct);
                await _tradeLogger.LogPortfolioSnapshotAsync(_portfolio, ct);

                _logger.LogInformation(
                    "[LivePoly] Trade RESOLVED: {Symbol} {Direction} Exit:{Exit:F2} PnL:{PnL:F4} Outcome:{Outcome}",
                    trade.Asset.Symbol, trade.Direction, exitPrice, pnl, outcome);
            }
        }

        // Periodic Portfolio sync from DB (prevents PnL drift)
        await SyncPortfolioFromDbAsync(ct);
    }

    private async Task SyncPortfolioFromDbAsync(CancellationToken ct)
    {
        try
        {
            var realPnL = await _tradeLogger.GetRealizedPnLAsync(EngineName, ct);
            var closedTrades = await _tradeLogger.GetClosedTradeCountsAsync(EngineName, ct);
            var openTrades = await _tradeLogger.GetOpenTradesAsync(EngineName, ct);
            var openExposure = openTrades?.Sum(t => t.PositionSize) ?? 0m;

            _portfolio.SyncFromDb(realPnL, closedTrades.wins, closedTrades.losses, openExposure);

            _logger.LogDebug(
                "[LivePoly] DB sync: PnL:{PnL:F4} W:{W} L:{L} OpenExposure:{Exp:F2} Balance:{Bal:F2}",
                realPnL, closedTrades.wins, closedTrades.losses, openExposure, _portfolio.Balance);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[LivePoly] Portfolio sync failed, will retry next cycle");
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
        _initLock.Dispose();
    }
}
