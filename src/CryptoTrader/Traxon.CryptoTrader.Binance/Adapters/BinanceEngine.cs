using System.Collections.Concurrent;
using Binance.Net.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Traxon.CryptoTrader.Application.Abstractions;
using Traxon.CryptoTrader.Binance.Abstractions;
using Traxon.CryptoTrader.Binance.Options;
using Traxon.CryptoTrader.Domain.Common;
using Traxon.CryptoTrader.Domain.Market;
using Traxon.CryptoTrader.Domain.Trading;

namespace Traxon.CryptoTrader.Binance.Adapters;

/// <summary>
/// Gercek Binance spot order execution motoru.
/// Disabled by default — BinanceOptions.Enabled=true olmadan hicbir order gondermez.
/// </summary>
public sealed class BinanceEngine : ITradingEngine
{
    private readonly IBinanceOrderService              _orderService;
    private readonly ITradeLogger                      _tradeLogger;
    private readonly IIndicatorCalculator              _indicatorCalculator;
    private readonly ICandleBuffer                     _candleBuffer;
    private readonly BinanceOptions                    _options;
    private readonly ILogger<BinanceEngine>            _logger;

    private readonly Portfolio                                              _portfolio;
    private readonly ConcurrentDictionary<Guid, Trade>                     _openTrades         = new();
    private readonly ConcurrentDictionary<Guid, long>                      _orderIdMap         = new();
    private readonly ConcurrentDictionary<Guid, Guid>                      _tradeToPositionMap = new();
    private readonly ConcurrentDictionary<Guid, (decimal sl, decimal tp)>  _slTpMap            = new();
    private readonly SemaphoreSlim                                          _lock               = new(1, 1);

    private const decimal TrackingInitialBalance = 100_000m;
    private const decimal SlippageRate           = 0.0005m;
    private const decimal SlMultiplier           = 1.5m;
    private const decimal TpMultiplier           = 2.0m;

    public string EngineName => "BinanceLive";

    public BinanceEngine(
        IBinanceOrderService orderService,
        ITradeLogger tradeLogger,
        IIndicatorCalculator indicatorCalculator,
        ICandleBuffer candleBuffer,
        IOptions<BinanceOptions> options,
        ILogger<BinanceEngine> logger)
    {
        _orderService        = orderService;
        _tradeLogger         = tradeLogger;
        _indicatorCalculator = indicatorCalculator;
        _candleBuffer        = candleBuffer;
        _options             = options.Value;
        _logger              = logger;
        _portfolio           = new Portfolio(EngineName, TrackingInitialBalance);
    }

    public Task<Result<bool>> IsReadyAsync(CancellationToken ct = default)
    {
        if (!_options.Enabled)
            return Task.FromResult(Result<bool>.Failure(
                new Error("BinanceEngine.Disabled", "Real trading is disabled. Set Binance:Enabled=true to activate.")));
        return Task.FromResult(Result<bool>.Success(true));
    }

    public async Task<Result<Trade>> OpenPositionAsync(
        Signal signal,
        CancellationToken ct = default)
    {
        if (!_options.Enabled)
            return Result<Trade>.Failure(
                new Error("BinanceEngine.Disabled", "Real trading is disabled."));

        var symbol = signal.Asset.Symbol;

        if (!_options.AllowedSymbols.Contains(symbol))
            return Result<Trade>.Failure(
                new Error("BinanceEngine.SymbolNotAllowed", $"Symbol {symbol} is not in AllowedSymbols whitelist."));

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

            var atr = atrResult.Value!.Value;

            // Gercek fiyati al
            var priceResult = await _orderService.GetCurrentPriceAsync(symbol, ct);
            if (priceResult.IsFailure)
                return Result<Trade>.Failure(priceResult.Error!);

            var entryPrice   = priceResult.Value * (1 + SlippageRate);
            var kellySize    = signal.KellyFraction * _portfolio.Balance;
            var positionSize = Math.Min(kellySize, _options.MaxPositionSize);
            var quantity     = positionSize / entryPrice;

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

            // Gercek order gonder
            var side        = signal.Direction == SignalDirection.Up ? OrderSide.Buy : OrderSide.Sell;
            var orderResult = await _orderService.PlaceMarketOrderAsync(symbol, side, quantity, ct);
            if (orderResult.IsFailure)
            {
                // Portfolio geri al
                _portfolio.ClosePosition(position.Id, 0m, TradeOutcome.Loss);
                return Result<Trade>.Failure(orderResult.Error!);
            }

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
                entryReason:       $"ATR:{atr:F6} SL:{sl:F4} TP:{tp:F4} OrderId:{orderResult.Value}");

            _openTrades[trade.Id]         = trade;
            _orderIdMap[trade.Id]         = orderResult.Value;
            _tradeToPositionMap[trade.Id] = position.Id;
            _slTpMap[trade.Id]            = (sl, tp);

            await _tradeLogger.LogTradeOpenedAsync(trade, ct);

            _logger.LogInformation(
                "[BinanceLive] Trade OPENED: {Symbol}/{Interval} {Direction} " +
                "Entry:{Entry:F4} SL:{SL:F4} TP:{TP:F4} Size:{Size:F2} OrderId:{OrderId}",
                trade.Asset.Symbol, trade.TimeFrame.Value, trade.Direction,
                entryPrice, sl, tp, positionSize, orderResult.Value);

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
            _orderIdMap.TryRemove(tradeId, out _);
            _tradeToPositionMap.TryRemove(tradeId, out var posId);

            return await CloseTradeInternalAsync(trade, posId, reason, ct);
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

    public async Task CheckPositionsAsync(Candle candle, CancellationToken ct = default)
    {
        var relevantTradeIds = _openTrades.Values
            .Where(t => t.Asset == candle.Asset && t.TimeFrame == candle.TimeFrame)
            .Select(t => t.Id)
            .ToList();

        foreach (var tradeId in relevantTradeIds)
        {
            if (!_openTrades.TryGetValue(tradeId, out var trade)) continue;
            if (!_slTpMap.TryGetValue(tradeId, out var slTp))    continue;

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

            if (!tpHit && !slHit) continue;

            var hitReason = tpHit ? "TakeProfit" : "StopLoss";

            if (_openTrades.TryRemove(tradeId, out _))
            {
                _slTpMap.TryRemove(tradeId, out _);
                _orderIdMap.TryRemove(tradeId, out _);
                _tradeToPositionMap.TryRemove(tradeId, out _);

                await _lock.WaitAsync(ct);
                try
                {
                    await CloseTradeInternalAsync(trade, default, hitReason, ct);
                }
                finally
                {
                    _lock.Release();
                }
            }
        }
    }

    private async Task<Result<Trade>> CloseTradeInternalAsync(
        Trade trade, Guid positionId, string reason, CancellationToken ct)
    {
        var symbol   = trade.Asset.Symbol;
        var quantity = trade.PositionSize / trade.EntryPrice;
        var side     = trade.Direction == SignalDirection.Up ? OrderSide.Sell : OrderSide.Buy;

        var orderResult = await _orderService.PlaceMarketOrderAsync(symbol, side, quantity, ct);

        // Gercek cikis fiyatini al
        var priceResult = await _orderService.GetCurrentPriceAsync(symbol, ct);
        var exitPrice   = priceResult.IsSuccess ? priceResult.Value : trade.EntryPrice;

        decimal rawPnl = trade.Direction == SignalDirection.Up
            ? (exitPrice - trade.EntryPrice) / trade.EntryPrice * trade.PositionSize
            : (trade.EntryPrice - exitPrice) / trade.EntryPrice * trade.PositionSize;

        var outcome = rawPnl >= 0 ? TradeOutcome.Win : TradeOutcome.Loss;
        trade.Close(exitPrice, outcome, rawPnl);

        if (positionId != default)
            _portfolio.ClosePosition(positionId, rawPnl, outcome);

        await _tradeLogger.LogTradeClosedAsync(trade, ct);
        await _tradeLogger.LogPortfolioSnapshotAsync(_portfolio, ct);

        if (orderResult.IsFailure)
        {
            _logger.LogError(
                "[BinanceLive] Close order FAILED for {Symbol} ({Reason}): {Error}",
                symbol, reason, orderResult.Error!.Message);
        }

        _logger.LogInformation(
            "[BinanceLive] Trade CLOSED ({Reason}): {Symbol}/{Interval} {Direction} " +
            "Exit:{Exit:F4} PnL:{PnL:F2}",
            reason, trade.Asset.Symbol, trade.TimeFrame.Value, trade.Direction,
            exitPrice, rawPnl);

        return Result<Trade>.Success(trade);
    }
}
