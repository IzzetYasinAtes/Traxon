using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Traxon.CryptoTrader.Application.Abstractions;
using Traxon.CryptoTrader.Domain.Common;
using Traxon.CryptoTrader.Domain.Market;
using Traxon.CryptoTrader.Domain.Trading;

namespace Traxon.CryptoTrader.Infrastructure.Engines;

/// <summary>
/// Polymarket binary-resolution simülasyonu yapan paper trading motoru.
/// Sanal bakiye $10,000 ile baslar. UP/DOWN pozisyon açar, TimeFrame süresi
/// dolduğunda candle yönüne göre WIN/LOSS resolve eder.
/// </summary>
public sealed class PaperPolymarketEngine : ITradingEngine
{
    private readonly ITradeLogger                   _tradeLogger;
    private readonly ILogger<PaperPolymarketEngine> _logger;

    private readonly Portfolio                              _portfolio;
    private readonly ConcurrentDictionary<Guid, Trade>     _openTrades        = new();
    private readonly ConcurrentDictionary<Guid, Guid>      _tradeToPositionMap = new();
    private readonly SemaphoreSlim                          _lock              = new(1, 1);
    private readonly SemaphoreSlim                          _initLock          = new(1, 1);
    private volatile bool                                   _initialized;

    private const decimal InitialBalance = 10_000m;
    private const decimal Slippage       = 0.01m;

    public string EngineName => "PaperPoly";

    public PaperPolymarketEngine(
        ITradeLogger tradeLogger,
        ILogger<PaperPolymarketEngine> logger)
    {
        _tradeLogger = tradeLogger;
        _logger      = logger;
        _portfolio   = new Portfolio(EngineName, InitialBalance);
    }

    /// <summary>
    /// Worker restart sonrasi in-memory _openTrades'i DB'den restore eder (bir kez).
    /// Bu sayede restart'tan sonra ayni asset icin duplicate trade acilmaz.
    /// </summary>
    private async Task EnsureInitializedAsync(CancellationToken ct)
    {
        if (_initialized) return;
        await _initLock.WaitAsync(ct);
        try
        {
            if (_initialized) return;
            var openTrades = await _tradeLogger.GetOpenTradesAsync(EngineName, ct);
            foreach (var trade in openTrades ?? [])
            {
                _openTrades[trade.Id] = trade;
                _tradeToPositionMap[trade.Id] = Guid.Empty; // position in-memory'de yok, portfolio etkisi olmaz
            }
            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task<Result<Trade>> OpenPositionAsync(
        Signal signal,
        CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);

        await _lock.WaitAsync(ct);
        try
        {
            if (_openTrades.Values.Any(t => t.Asset == signal.Asset))
                return Result<Trade>.Failure(Error.DuplicatePosition);

            var entryPrice   = signal.MarketPrice + Slippage;
            var positionSize = Math.Min(signal.KellyFraction * _portfolio.Balance, _portfolio.MaxPositionSize);

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

            var indicatorJson = System.Text.Json.JsonSerializer.Serialize(new
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
                entryReason:       $"FV:{signal.FairValue:F3} Edge:{signal.Edge:F3} Bulls:{signal.Indicators.BullishCount()}/5");

            _openTrades[trade.Id]          = trade;
            _tradeToPositionMap[trade.Id]  = position.Id;

            await _tradeLogger.LogTradeOpenedAsync(trade, ct);

            _logger.LogInformation(
                "[PaperPoly] Trade OPENED: {Symbol}/{Interval} {Direction} Entry:{Entry:F4} Size:{Size:F2}",
                trade.Asset.Symbol, trade.TimeFrame.Value, trade.Direction,
                trade.EntryPrice, trade.PositionSize);

            return Result<Trade>.Success(trade);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<Result<Trade>> ClosePositionAsync(
        Guid tradeId,
        string reason,
        CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (!_openTrades.TryRemove(tradeId, out var trade))
                return Result<Trade>.Failure(Error.TradeNotFound);

            _tradeToPositionMap.TryRemove(tradeId, out var posId);

            return await ResolveTradeAsync(trade, posId, isWin: false, ct);
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

    public Task<Result<bool>> IsReadyAsync(CancellationToken ct = default)
        => Task.FromResult(Result<bool>.Success(true));

    public async Task CheckPositionsAsync(Candle candle, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        var expiredTradeIds = _openTrades.Values
            .Where(t => t.Asset == candle.Asset
                     && t.TimeFrame == candle.TimeFrame
                     && now - t.OpenedAt >= candle.TimeFrame.Duration)
            .Select(t => t.Id)
            .ToList();

        foreach (var tradeId in expiredTradeIds)
        {
            if (!_openTrades.TryRemove(tradeId, out var trade))
                continue;

            _tradeToPositionMap.TryRemove(tradeId, out var posId);

            var isUp     = candle.Close >= candle.Open;
            var tradeWon = (trade.Direction == SignalDirection.Up   && isUp)
                        || (trade.Direction == SignalDirection.Down && !isUp);

            await _lock.WaitAsync(ct);
            try
            {
                await ResolveTradeAsync(trade, posId, tradeWon, ct);
            }
            finally
            {
                _lock.Release();
            }
        }
    }

    private async Task<Result<Trade>> ResolveTradeAsync(
        Trade trade, Guid positionId, bool isWin, CancellationToken ct)
    {
        decimal exitPrice, pnl;
        TradeOutcome outcome;

        if (isWin)
        {
            outcome   = TradeOutcome.Win;
            exitPrice = 1.00m;
            pnl       = (1.00m - trade.EntryPrice) / trade.EntryPrice * trade.PositionSize;
        }
        else
        {
            outcome   = TradeOutcome.Loss;
            exitPrice = 0.00m;
            pnl       = -trade.PositionSize;
        }

        trade.Close(exitPrice, outcome, pnl);
        _portfolio.ClosePosition(positionId, pnl, outcome);

        await _tradeLogger.LogTradeClosedAsync(trade, ct);
        await _tradeLogger.LogPortfolioSnapshotAsync(_portfolio, ct);

        _logger.LogInformation(
            "[PaperPoly] Trade CLOSED: {Symbol}/{Interval} {Direction} {Outcome} PnL:{PnL:F2} " +
            "Balance:{Balance:F2} WinRate:{WR:P0}",
            trade.Asset.Symbol, trade.TimeFrame.Value, trade.Direction,
            outcome, pnl, _portfolio.Balance, _portfolio.WinRate);

        return Result<Trade>.Success(trade);
    }
}
