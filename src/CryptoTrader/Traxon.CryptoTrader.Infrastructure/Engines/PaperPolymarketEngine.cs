using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Traxon.CryptoTrader.Application.Abstractions;
using Traxon.CryptoTrader.Domain.Common;
using Traxon.CryptoTrader.Domain.Market;
using Traxon.CryptoTrader.Domain.Trading;
using Traxon.CryptoTrader.Polymarket.Options;

namespace Traxon.CryptoTrader.Infrastructure.Engines;

/// <summary>
/// Paper trading motoru — LivePoly ile birebir aynı mantık, tek fark gerçek order gönderilmez.
/// Gamma API'den gerçek market keşfi, CLOB'dan gerçek midpoint fiyat alır.
/// Pozisyon resolve: fiyat >= 0.95 → WIN, fiyat <= 0.05 → LOSS.
/// </summary>
public sealed class PaperPolymarketEngine : ITradingEngine
{
    private readonly IPolymarketClient              _client;
    private readonly IMarketDiscoveryService        _discovery;
    private readonly PolymarketOptions              _options;
    private readonly ITradeLogger                   _tradeLogger;
    private readonly ILogger<PaperPolymarketEngine> _logger;

    private readonly Portfolio                              _portfolio;
    private readonly ConcurrentDictionary<Guid, Trade>     _openTrades    = new();
    private readonly ConcurrentDictionary<Guid, string>    _tradeToTokenId = new();
    private readonly ConcurrentDictionary<Guid, Guid>      _tradeToPositionMap = new();
    private readonly SemaphoreSlim                          _lock          = new(1, 1);
    private readonly SemaphoreSlim                          _initLock      = new(1, 1);
    private volatile bool                                   _initialized;

    private const decimal InitialBalance = 20m;
    private const decimal FeeRate        = 0.072m; // Polymarket crypto taker fee rate

    // Polymarket sadece bu asset'ler için market açar
    private static readonly HashSet<string> SupportedAssets = new(StringComparer.OrdinalIgnoreCase)
    {
        "BTC", "ETH", "SOL", "XRP", "DOGE", "BNB", "HYPE"
    };

    public string EngineName => "PaperPoly";

    public PaperPolymarketEngine(
        IPolymarketClient client,
        IMarketDiscoveryService discovery,
        IOptions<PolymarketOptions> options,
        ITradeLogger tradeLogger,
        ILogger<PaperPolymarketEngine> logger)
    {
        _client      = client;
        _discovery   = discovery;
        _options     = options.Value;
        _tradeLogger = tradeLogger;
        _logger      = logger;
        _portfolio   = new Portfolio(EngineName, InitialBalance);
    }

    /// <summary>
    /// Worker restart sonrasi in-memory _openTrades'i DB'den restore eder (bir kez).
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
                var winCount  = snapshot.WinRate.HasValue && snapshot.TradeCount > 0
                    ? (int)Math.Round(snapshot.WinRate.Value * snapshot.TradeCount)
                    : 0;
                var lossCount = snapshot.TradeCount - winCount;
                _portfolio.Restore(snapshot.Balance, snapshot.TotalPnL, winCount, lossCount);
                _logger.LogInformation(
                    "[PaperPoly] Portfolio restored: Balance:{Balance:F2} PnL:{PnL:F2} W:{Win} L:{Loss}",
                    snapshot.Balance, snapshot.TotalPnL, winCount, lossCount);
            }

            var openTrades = await _tradeLogger.GetOpenTradesAsync(EngineName, ct);
            foreach (var trade in openTrades ?? [])
            {
                _openTrades[trade.Id] = trade;

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
        => Task.FromResult(Result<bool>.Success(true));

    public async Task<Result<Trade>> OpenPositionAsync(
        Signal signal, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);

        await _lock.WaitAsync(ct);
        try
        {
            if (_openTrades.Values.Any(t => t.Asset == signal.Asset))
                return Result<Trade>.Failure(Error.DuplicatePosition);

            // Fast pre-check: Polymarket sadece belirli asset'ler için market açar
            var baseAsset = signal.Asset.Symbol.Replace("USDT", "");
            if (!SupportedAssets.Contains(baseAsset))
                return Result<Trade>.Failure(Error.MarketNotFound);

            // 1. Gamma API'den gerçek market bul — LivePoly satır 75-83
            var discoverResult = await _discovery.DiscoverMarketsAsync(ct);
            if (discoverResult.IsFailure)
                return Result<Trade>.Failure(discoverResult.Error!);

            var targetDirection = signal.Direction == SignalDirection.Up ? "Up" : "Down";
            var market = discoverResult.Value!
                .FirstOrDefault(m => m.UnderlyingAsset.Equals(
                    signal.Asset.Symbol.Replace("USDT", ""),
                    StringComparison.OrdinalIgnoreCase)
                    && m.Direction == targetDirection);

            if (market is null)
            {
                _logger.LogWarning("[PaperPoly] No Polymarket market found for {Asset} {Direction}",
                    signal.Asset.Symbol, signal.Direction);
                return Result<Trade>.Failure(Error.MarketNotFound);
            }

            // 2. CLOB'dan gerçek midpoint fiyat al — LivePoly satır 93
            var midpointResult = await _client.GetMidpointAsync(market.RelevantTokenId, ct);
            if (midpointResult.IsFailure)
                return Result<Trade>.Failure(midpointResult.Error!);

            var marketPrice = midpointResult.Value;

            // 3. Position size = bakiyenin %2'si, minimum $1
            var positionSize = Math.Max(_portfolio.Balance * 0.02m, 1m);

            // 4. Market order — midpoint fiyattan alınır (taker)
            var entryPrice = marketPrice;

            // 5. Fee hesapla: shares * 0.072 * p * (1-p)
            var shares = positionSize / entryPrice;
            var fee = shares * FeeRate * entryPrice * (1m - entryPrice);

            _logger.LogInformation(
                "[PaperPoly] SIMULATED market order: {TokenId} BUY @ {Price:F4} Size:{Size:F2} Fee:{Fee:F4}",
                market.RelevantTokenId, entryPrice, positionSize, fee);

            // 6. Trade oluştur, DB'ye kaydet — LivePoly ile aynı
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
                entryReason:       $"FV:{signal.FairValue:F3} Edge:{signal.Edge:F3} Token:{market.RelevantTokenId}");

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

            _openTrades[trade.Id]          = trade;
            _tradeToTokenId[trade.Id]      = market.RelevantTokenId;
            _tradeToPositionMap[trade.Id]  = position.Id;

            await _tradeLogger.LogTradeOpenedAsync(trade, ct);
            await _tradeLogger.LogPortfolioSnapshotAsync(_portfolio, ct);

            _logger.LogInformation(
                "[PaperPoly] Trade OPENED: {Symbol} {Direction} Entry:{Entry:F4} Size:{Size:F2} MidPrice:{Mid:F4}",
                trade.Asset.Symbol, trade.Direction, trade.EntryPrice, trade.PositionSize, marketPrice);

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

            _tradeToTokenId.TryRemove(tradeId, out _);
            _tradeToPositionMap.TryRemove(tradeId, out var posId);

            trade.Close(0m, TradeOutcome.Loss, -trade.PositionSize);
            _portfolio.ClosePosition(posId, -trade.PositionSize, TradeOutcome.Loss);

            await _tradeLogger.LogTradeClosedAsync(trade, ct);
            await _tradeLogger.LogPortfolioSnapshotAsync(_portfolio, ct);

            _logger.LogInformation(
                "[PaperPoly] Trade CLOSED: {Symbol} {Direction} Reason:{Reason}",
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
    /// (fiyat >= 0.95 veya <= 0.05) trade'i kapatır — LivePoly ile birebir aynı mantık.
    /// </summary>
    public async Task CheckPositionsAsync(Candle candle, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);

        if (_openTrades.IsEmpty) return;

        var tradesToClose = new List<(Guid tradeId, decimal exitPrice, TradeOutcome outcome, decimal pnl)>();

        // Fetch ALL markets (including closed) once for all trades
        var discoverResult = await _discovery.DiscoverAllMarketsAsync(ct);
        if (discoverResult.IsFailure)
        {
            _logger.LogWarning("[PaperPoly] DiscoverAllMarkets failed: {Error}", discoverResult.Error?.Message);
            return;
        }

        var allMarkets = discoverResult.Value!;

        foreach (var (tradeId, trade) in _openTrades)
        {
            try
            {
                // Match by exact token ID to find the specific market this trade was opened on
                _tradeToTokenId.TryGetValue(tradeId, out var tokenId);
                if (string.IsNullOrEmpty(tokenId)) continue;

                var market = allMarkets
                    .FirstOrDefault(m => m.YesTokenId == tokenId || m.NoTokenId == tokenId);

                if (market is null) continue;

                // Market resolved (closed with outcome price)
                if (market.ResolvedPrice.HasValue)
                {
                    // Fee hesapla: shares * 0.072 * p * (1-p) — alırken ödendi
                    var tradeShares = trade.PositionSize / trade.EntryPrice;
                    var tradeFee = tradeShares * FeeRate * trade.EntryPrice * (1m - trade.EntryPrice);

                    if (market.ResolvedPrice.Value >= 0.99m)
                    {
                        // WIN — payout: shares * $1.00
                        var payout = tradeShares * 1.0m;
                        var pnl = payout - trade.PositionSize - tradeFee;
                        tradesToClose.Add((tradeId, 1.0m, TradeOutcome.Win, pnl));
                    }
                    else
                    {
                        // LOSS — payout: $0, kaybedilen: yatırım + fee
                        var pnl = -(trade.PositionSize + tradeFee);
                        tradesToClose.Add((tradeId, 0.0m, TradeOutcome.Loss, pnl));
                    }

                    _logger.LogInformation(
                        "[PaperPoly] Market RESOLVED for {Asset} {Direction}: ResolvedPrice={Price}",
                        trade.Asset.Symbol, trade.Direction, market.ResolvedPrice.Value);
                    continue;
                }

                // Market still open — use midpoint price
                var midpointResult = await _client.GetMidpointAsync(market.RelevantTokenId, ct);
                if (midpointResult.IsFailure) continue;

                var currentPrice = midpointResult.Value;

                if (currentPrice >= 0.95m)
                {
                    var s = trade.PositionSize / trade.EntryPrice;
                    var f = s * FeeRate * trade.EntryPrice * (1m - trade.EntryPrice);
                    var pnl = (s * 1.0m) - trade.PositionSize - f;
                    tradesToClose.Add((tradeId, 1.0m, TradeOutcome.Win, pnl));
                }
                else if (currentPrice <= 0.05m)
                {
                    var s = trade.PositionSize / trade.EntryPrice;
                    var f = s * FeeRate * trade.EntryPrice * (1m - trade.EntryPrice);
                    var pnl = -(trade.PositionSize + f);
                    tradesToClose.Add((tradeId, 0.0m, TradeOutcome.Loss, pnl));
                }
                // 0.05 < price < 0.95 → not resolved yet, WAIT
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[PaperPoly] CheckPositions error for trade {TradeId}", tradeId);
            }
        }

        // Resolve edilen trade'leri kapat — LivePoly satır 252-267
        foreach (var (tradeId, exitPrice, outcome, pnl) in tradesToClose)
        {
            if (_openTrades.TryRemove(tradeId, out var trade))
            {
                _tradeToTokenId.TryRemove(tradeId, out _);
                _tradeToPositionMap.TryRemove(tradeId, out var posId);

                trade.Close(exitPrice, outcome, pnl);
                _portfolio.ClosePosition(posId, pnl, outcome);

                await _tradeLogger.LogTradeClosedAsync(trade, ct);
                await _tradeLogger.LogPortfolioSnapshotAsync(_portfolio, ct);

                _logger.LogInformation(
                    "[PaperPoly] Trade RESOLVED: {Symbol} {Direction} Exit:{Exit:F2} PnL:{PnL:F4} Outcome:{Outcome}",
                    trade.Asset.Symbol, trade.Direction, exitPrice, pnl, outcome);
            }
        }
    }
}
