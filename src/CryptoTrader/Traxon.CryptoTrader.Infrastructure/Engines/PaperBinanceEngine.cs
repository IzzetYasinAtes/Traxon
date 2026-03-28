using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Traxon.CryptoTrader.Application.Abstractions;
using Traxon.CryptoTrader.Domain.Common;
using Traxon.CryptoTrader.Domain.Market;
using Traxon.CryptoTrader.Domain.Trading;

namespace Traxon.CryptoTrader.Infrastructure.Engines;

/// <summary>
/// Binance spot simülasyonu yapan paper trading motoru.
/// ATR tabanlı SL/TP kullanır. Her candle kapanışında SL/TP hit kontrolü yapar.
/// </summary>
public sealed class PaperBinanceEngine : ITradingEngine
{
    private readonly ITradeLogger                  _tradeLogger;
    private readonly IIndicatorCalculator          _indicatorCalculator;
    private readonly ICandleBuffer                 _candleBuffer;
    private readonly ILogger<PaperBinanceEngine>   _logger;

    private readonly Portfolio                                           _portfolio;
    private readonly ConcurrentDictionary<Guid, Trade>                  _openTrades         = new();
    private readonly ConcurrentDictionary<Guid, Guid>                   _tradeToPositionMap = new();
    private readonly ConcurrentDictionary<Guid, (decimal sl, decimal tp)> _slTpMap          = new();
    private readonly SemaphoreSlim                                       _lock               = new(1, 1);
    private readonly SemaphoreSlim                                       _initLock           = new(1, 1);
    private volatile bool                                                _initialized;

    private sealed record SlTpSnapshot(
        [property: System.Text.Json.Serialization.JsonPropertyName("sl")] decimal Sl,
        [property: System.Text.Json.Serialization.JsonPropertyName("tp")] decimal Tp);

    private const decimal InitialBalance = 10_000m;
    private const decimal SlippageRate   = 0.0005m;
    private const decimal SlMultiplier   = 1.5m;
    private const decimal TpMultiplier   = 2.0m;

    public string EngineName => "PaperBinance";

    public PaperBinanceEngine(
        ITradeLogger tradeLogger,
        IIndicatorCalculator indicatorCalculator,
        ICandleBuffer candleBuffer,
        ILogger<PaperBinanceEngine> logger)
    {
        _tradeLogger         = tradeLogger;
        _indicatorCalculator = indicatorCalculator;
        _candleBuffer        = candleBuffer;
        _logger              = logger;
        _portfolio           = new Portfolio(EngineName, InitialBalance);
    }

    /// <summary>
    /// Worker restart sonrasi in-memory state'i DB'den restore eder (bir kez).
    /// _openTrades ve _slTpMap restore edilerek SL/TP kontrolu da calisir.
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
                _tradeToPositionMap[trade.Id] = Guid.Empty; // position in-memory'de yok

                try
                {
                    var snap = System.Text.Json.JsonSerializer.Deserialize<SlTpSnapshot>(trade.IndicatorSnapshot);
                    if (snap is not null)
                        _slTpMap[trade.Id] = (snap.Sl, snap.Tp);
                }
                catch
                {
                    // JSON parse hatasi: trade duplicate engeller ama SL/TP kontrolu calismaz
                }
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

            var candlesResult = _candleBuffer.GetAll(signal.Asset, signal.TimeFrame);
            if (candlesResult.IsFailure)
                return Result<Trade>.Failure(Error.EngineNotReady);

            var atrResult = _indicatorCalculator.CalculateAtr(candlesResult.Value!);
            if (atrResult.IsFailure)
                return Result<Trade>.Failure(Error.EngineNotReady);

            var atr          = atrResult.Value!.Value;
            var lastCandle   = candlesResult.Value![^1];
            var entryPrice   = lastCandle.Close * (1 + SlippageRate);
            var positionSize = Math.Min(signal.KellyFraction * _portfolio.Balance, _portfolio.MaxPositionSize);

            var sl = signal.Direction == SignalDirection.Up
                ? entryPrice - (SlMultiplier * atr)
                : entryPrice + (SlMultiplier * atr);

            var tp = signal.Direction == SignalDirection.Up
                ? entryPrice + (TpMultiplier * atr)
                : entryPrice - (TpMultiplier * atr);

            var position = new Position(
                asset:        signal.Asset,
                timeFrame:    signal.TimeFrame,
                direction:    signal.Direction,
                entryPrice:   entryPrice,
                positionSize: positionSize,
                stopLoss:     sl,
                takeProfit:   tp);

            var openResult = _portfolio.OpenPosition(position);
            if (openResult.IsFailure)
                return Result<Trade>.Failure(openResult.Error!);

            var indicatorJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                rsi       = signal.Indicators.Rsi.Value,
                macd_hist = signal.Indicators.Macd.Histogram,
                atr,
                sl,
                tp,
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
                entryReason:       $"ATR:{atr:F6} SL:{sl:F4} TP:{tp:F4}");

            _openTrades[trade.Id]         = trade;
            _tradeToPositionMap[trade.Id] = position.Id;
            _slTpMap[trade.Id]            = (sl, tp);

            await _tradeLogger.LogTradeOpenedAsync(trade, ct);

            _logger.LogInformation(
                "[PaperBinance] Trade OPENED: {Symbol}/{Interval} {Direction} " +
                "Entry:{Entry:F4} SL:{SL:F4} TP:{TP:F4} Size:{Size:F2}",
                trade.Asset.Symbol, trade.TimeFrame.Value, trade.Direction,
                entryPrice, sl, tp, positionSize);

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

            _slTpMap.TryRemove(tradeId, out _);
            _tradeToPositionMap.TryRemove(tradeId, out var posId);

            var candlesResult = _candleBuffer.GetAll(trade.Asset, trade.TimeFrame);
            var exitPrice = candlesResult.IsSuccess
                ? candlesResult.Value![^1].Close
                : trade.EntryPrice;

            return await CloseTradeInternalAsync(trade, posId, exitPrice, reason, ct);
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
        var relevantTradeIds = _openTrades.Values
            .Where(t => t.Asset == candle.Asset && t.TimeFrame == candle.TimeFrame)
            .Select(t => t.Id)
            .ToList();

        _logger.LogDebug(
            "[PaperBinance] CheckPositions {Symbol}/{Interval} candle.OpenTime:{OpenTime} relevant:{Count}",
            candle.Asset.Symbol, candle.TimeFrame.Value, candle.OpenTime, relevantTradeIds.Count);

        foreach (var tradeId in relevantTradeIds)
        {
            if (!_openTrades.TryGetValue(tradeId, out var trade))         continue;
            if (!_tradeToPositionMap.TryGetValue(tradeId, out var posId)) continue;

            string reason;
            decimal exitPrice;

            if (_slTpMap.TryGetValue(tradeId, out var slTp))
            {
                var (sl, tp) = slTp;
                bool tpHit, slHit;

                if (trade.Direction == SignalDirection.Up)
                {
                    tpHit = candle.High >= tp;
                    slHit = candle.Low  <= sl;
                }
                else
                {
                    tpHit = candle.Low  <= tp;
                    slHit = candle.High >= sl;
                }

                // Max-hold safety: force-close after 3 full timeframe durations
                // (protects against wide ATR levels that are never reached)
                var maxHold = candle.OpenTime - trade.OpenedAt >= candle.TimeFrame.Duration * 3;

                if (!tpHit && !slHit && !maxHold) continue;

                exitPrice = tpHit ? tp : slHit ? sl : candle.Close;
                reason    = tpHit ? "TakeProfit" : slHit ? "StopLoss" : "MaxHold";
            }
            else
            {
                // SL/TP not available (restored trade with missing snapshot) — use max-hold only
                if (candle.OpenTime - trade.OpenedAt < candle.TimeFrame.Duration * 3) continue;
                exitPrice = candle.Close;
                reason    = "MaxHold";
            }

            if (_openTrades.TryRemove(tradeId, out _))
            {
                _slTpMap.TryRemove(tradeId, out _);
                _tradeToPositionMap.TryRemove(tradeId, out _);

                await _lock.WaitAsync(ct);
                try
                {
                    await CloseTradeInternalAsync(trade, posId, exitPrice, reason, ct);
                }
                finally
                {
                    _lock.Release();
                }
            }
        }
    }

    private async Task<Result<Trade>> CloseTradeInternalAsync(
        Trade trade, Guid positionId, decimal exitPrice, string reason, CancellationToken ct)
    {
        decimal rawPnl = trade.Direction == SignalDirection.Up
            ? (exitPrice - trade.EntryPrice) / trade.EntryPrice * trade.PositionSize
            : (trade.EntryPrice - exitPrice) / trade.EntryPrice * trade.PositionSize;

        var outcome = rawPnl >= 0 ? TradeOutcome.Win : TradeOutcome.Loss;
        trade.Close(exitPrice, outcome, rawPnl);
        _portfolio.ClosePosition(positionId, rawPnl, outcome);

        await _tradeLogger.LogTradeClosedAsync(trade, ct);
        await _tradeLogger.LogPortfolioSnapshotAsync(_portfolio, ct);

        _logger.LogInformation(
            "[PaperBinance] Trade CLOSED ({Reason}): {Symbol}/{Interval} {Direction} " +
            "Exit:{Exit:F4} PnL:{PnL:F2} Balance:{Balance:F2} WinRate:{WR:P0}",
            reason, trade.Asset.Symbol, trade.TimeFrame.Value, trade.Direction,
            exitPrice, rawPnl, _portfolio.Balance, _portfolio.WinRate);

        return Result<Trade>.Success(trade);
    }
}
