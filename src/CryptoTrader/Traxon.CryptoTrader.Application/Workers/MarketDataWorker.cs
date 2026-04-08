using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Traxon.CryptoTrader.Application.Abstractions;
using Traxon.CryptoTrader.Application.DTOs;
using Traxon.CryptoTrader.Application.Mappings;
using Traxon.CryptoTrader.Domain.Assets;
using Traxon.CryptoTrader.Domain.Market;
using Traxon.CryptoTrader.Domain.Trading;

namespace Traxon.CryptoTrader.Application.Workers;

public sealed class MarketDataWorker : BackgroundService
{
    private readonly IMarketDataProvider         _marketDataProvider;
    private readonly ICandleBuffer               _candleBuffer;
    private readonly IIndicatorCalculator        _indicatorCalculator;
    private readonly ISignalGenerator            _signalGenerator;
    private readonly IEnumerable<ITradingEngine> _tradingEngines;
    private readonly ICandleWriter               _candleWriter;
    private readonly IMarketEventPublisher       _publisher;
    private readonly ITradeLogger                _tradeLogger;
    private readonly IPolymarketClient           _polyClient;
    private readonly IMarketDiscoveryService     _discovery;
    private readonly ILogger<MarketDataWorker>   _logger;

    private const int BackfillDays = 3;
    private const int BackfillPageSize = 1500;
    private const int MinOneMinuteCandles = 100;

    public MarketDataWorker(
        IMarketDataProvider marketDataProvider,
        ICandleBuffer candleBuffer,
        IIndicatorCalculator indicatorCalculator,
        ISignalGenerator signalGenerator,
        IEnumerable<ITradingEngine> tradingEngines,
        ICandleWriter candleWriter,
        IMarketEventPublisher publisher,
        ITradeLogger tradeLogger,
        IPolymarketClient polyClient,
        IMarketDiscoveryService discovery,
        ILogger<MarketDataWorker> logger)
    {
        _marketDataProvider  = marketDataProvider;
        _candleBuffer        = candleBuffer;
        _indicatorCalculator = indicatorCalculator;
        _signalGenerator     = signalGenerator;
        _tradingEngines      = tradingEngines;
        _candleWriter        = candleWriter;
        _publisher           = publisher;
        _tradeLogger         = tradeLogger;
        _polyClient          = polyClient;
        _discovery           = discovery;
        _logger              = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MarketDataWorker starting — loading historical 1m candles (paginated {Days}-day backfill)...", BackfillDays);

        await BackfillOneMinuteCandlesAsync(stoppingToken);

        _logger.LogInformation("Buffer warm-up complete. Starting WebSocket stream (1m only)...");

        var engineCount = _tradingEngines.Count();
        _publisher.PublishSystemStatus(new SystemStatusDto(
            IsRunning: true,
            IsBinanceConnected: true,
            ActiveEngineCount: engineCount,
            StartedAt: DateTime.UtcNow));

        await _marketDataProvider.StartStreamAsync(
            assets: Asset.Tradeable,
            timeFrames: TimeFrame.All,
            onCandleClosed: OnCandleClosedAsync,
            cancellationToken: stoppingToken);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task BackfillOneMinuteCandlesAsync(CancellationToken ct)
    {
        var startTime = DateTime.UtcNow.AddDays(-BackfillDays);

        foreach (var asset in Asset.Tradeable)
        {
            var currentStart = startTime;
            var totalLoaded = 0;

            while (currentStart < DateTime.UtcNow)
            {
                var candlesResult = await _marketDataProvider.GetHistoricalCandlesAsync(
                    asset, TimeFrame.OneMinute, BackfillPageSize, currentStart, ct);

                if (candlesResult.IsFailure)
                {
                    _logger.LogWarning("Failed to load 1m candles for {Symbol} from {Start}: {Error}",
                        asset.Symbol, currentStart, candlesResult.Error!.Message);
                    break;
                }

                var candles = candlesResult.Value!;
                if (candles.Count == 0) break;

                foreach (var candle in candles)
                {
                    _candleBuffer.Add(candle);
                    _ = _candleWriter.WriteAsync(candle, ct);
                }

                totalLoaded += candles.Count;
                currentStart = candles[^1].CloseTime;

                if (candles.Count < BackfillPageSize) break;
            }

            _logger.LogInformation("Backfilled {Count} 1m candles for {Symbol}", totalLoaded, asset.Symbol);
        }
    }

    private async Task OnCandleClosedAsync(Candle candle)
    {
        _candleBuffer.Add(candle);
        PublishTickerUpdate(candle);
        _publisher.PublishCandleUpdate(candle.ToCandleDto());
        WriteCandleAsync(candle);

        // Fires at window boundary (:05, :10, etc.). We analyze the PREVIOUS completed
        // 5-min window and enter the NEW window immediately at T=0.
        // Candle OpenTime=:04 closes at :05 -> (4+1)%5==0 triggers signal pipeline.
        if ((candle.OpenTime.Minute + 1) % 5 == 0 && candle.OpenTime.Second == 0)
            await RunSignalPipelineAsync(candle);

        foreach (var engine in _tradingEngines)
            await engine.CheckPositionsAsync(candle);
    }

    private void PublishTickerUpdate(Candle candle)
    {
        decimal change = 0m;
        decimal changePercent = 0m;
        var bufferResult = _candleBuffer.GetAll(candle.Asset, candle.TimeFrame);
        if (bufferResult.IsSuccess)
        {
            var all = bufferResult.Value!;
            if (all.Count >= 2)
            {
                var previousClose = all[^2].Close;
                change = candle.Close - previousClose;
                changePercent = previousClose > 0 ? change / previousClose * 100m : 0m;
            }
        }
        _publisher.PublishTickerUpdate(new TickerDto(
            candle.Asset.Symbol, candle.Close, change, changePercent, DateTime.UtcNow));
    }

    private void WriteCandleAsync(Candle candle)
    {
        _ = Task.Run(async () =>
        {
            try { await _candleWriter.WriteAsync(candle); }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Candle write failed for {Symbol}/{TF}, signal generation continues",
                    candle.Asset.Symbol, candle.TimeFrame.Value);
            }
        });
    }

    private async Task RunSignalPipelineAsync(Candle candle)
    {
        if (!_candleBuffer.IsWarmedUp(candle.Asset, TimeFrame.OneMinute, minimumCandles: MinOneMinuteCandles))
        {
            _logger.LogDebug("Buffer not warmed up yet for {Symbol}/1m", candle.Asset.Symbol);
            return;
        }

        var candlesResult = _candleBuffer.GetAll(candle.Asset, TimeFrame.OneMinute);
        if (candlesResult.IsFailure) return;

        var oneMinCandles = candlesResult.Value!;

        var indicatorResult = _indicatorCalculator.Calculate(
            candle.Asset, TimeFrame.OneMinute, oneMinCandles);

        if (indicatorResult.IsFailure)
        {
            _logger.LogWarning("Indicator calc failed for {Symbol}/1m: {Error}",
                candle.Asset.Symbol, indicatorResult.Error!.Message);
            return;
        }

        var indicators = indicatorResult.Value!;
        _logger.LogInformation(
            "{Symbol}/1m — RSI:{Rsi:F1} MACD:{Macd:F6} BB({Lower:F2}/{Upper:F2}) ATR:{Atr:F6} Bulls:{Bulls}/5",
            candle.Asset.Symbol,
            indicators.Rsi.Value,
            indicators.Macd.Histogram,
            indicators.BollingerBands.Lower, indicators.BollingerBands.Upper,
            indicators.Atr.Value,
            indicators.BullishCount());

        await TryGenerateAndDispatchSignalAsync(candle, oneMinCandles, indicators);
    }

    private async Task TryGenerateAndDispatchSignalAsync(
        Candle candle,
        IReadOnlyList<Candle> oneMinCandles,
        Domain.Indicators.TechnicalIndicators indicators)
    {
        if (oneMinCandles.Count < 250) return; // Need enough for rolling autocorrelation + SMAs

        // ======================================================
        // LOOP 13: AUTOCORRELATION + OFI DELTA + MULTI-TF FILTER
        // ======================================================

        // === STEP 1: Compute rolling 5-minute returns ===
        // Each "5m return" = cumulative return over 5 consecutive 1m candles
        var fiveMinReturns = new List<decimal>();
        for (int end = oneMinCandles.Count - 5; end >= 5; end -= 5)
        {
            var open = oneMinCandles[end].Open;
            var close = oneMinCandles[end + 4].Close;
            if (open > 0)
                fiveMinReturns.Add((close - open) / open);
            if (fiveMinReturns.Count >= 100) break;
        }
        fiveMinReturns.Reverse(); // oldest first

        // === STEP 2: Compute rolling autocorrelation (lag-1) ===
        // This tells us: does this asset mean-revert or follow momentum at 5m scale?
        var autocorr = 0m;
        if (fiveMinReturns.Count >= 20)
        {
            var n = fiveMinReturns.Count - 1;
            var meanR = fiveMinReturns.Take(n).Average();
            var meanR1 = fiveMinReturns.Skip(1).Average();
            var covSum = 0m; var var0Sum = 0m; var var1Sum = 0m;
            for (int i = 0; i < n; i++)
            {
                var r0 = fiveMinReturns[i] - meanR;
                var r1 = fiveMinReturns[i + 1] - meanR1;
                covSum += r0 * r1;
                var0Sum += r0 * r0;
                var1Sum += r1 * r1;
            }
            var denom = (decimal)Math.Sqrt((double)(var0Sum * var1Sum));
            autocorr = denom > 0 ? covSum / denom : 0m;
        }

        // Previous 5-minute return (what just happened)
        var prev5mReturn = oneMinCandles[^5].Open > 0
            ? (oneMinCandles[^1].Close - oneMinCandles[^5].Open) / oneMinCandles[^5].Open
            : 0m;

        // Autocorrelation-based direction prediction
        decimal scoreAutocorr;
        if (Math.Abs(autocorr) < 0.03m)
        {
            // Random walk regime for this asset — autocorrelation too weak
            scoreAutocorr = 0m;
        }
        else if (autocorr < 0)
        {
            // Mean reverting: FADE the previous move
            scoreAutocorr = -Math.Sign(prev5mReturn) * Math.Clamp(Math.Abs(prev5mReturn) * 200m, 0m, 1m);
        }
        else
        {
            // Momentum: FOLLOW the previous move
            scoreAutocorr = Math.Sign(prev5mReturn) * Math.Clamp(Math.Abs(prev5mReturn) * 200m, 0m, 1m);
        }

        // Only trade when previous move was significant
        if (Math.Abs(prev5mReturn) < 0.0010m) // minimum 0.10% move
            scoreAutocorr *= 0.3m; // heavily discount weak moves

        // === STEP 3: OFI Delta (CHANGE in order flow, not level) ===
        var ofiRecent = 0m; var volRecent = 0m;
        for (int i = oneMinCandles.Count - 2; i < oneMinCandles.Count; i++)
        {
            ofiRecent += oneMinCandles[i].TakerBuyBaseVolume;
            volRecent += oneMinCandles[i].Volume;
        }
        var ofiRecentRatio = volRecent > 0 ? ofiRecent / volRecent : 0.5m;

        var ofiBaseline = 0m; var volBaseline = 0m;
        for (int i = oneMinCandles.Count - 7; i < oneMinCandles.Count - 2; i++)
        {
            ofiBaseline += oneMinCandles[i].TakerBuyBaseVolume;
            volBaseline += oneMinCandles[i].Volume;
        }
        var ofiBaselineRatio = volBaseline > 0 ? ofiBaseline / volBaseline : 0.5m;

        var ofiDelta = ofiRecentRatio - ofiBaselineRatio;
        var scoreOFIDelta = Math.Clamp(ofiDelta * 15m, -1m, 1m);

        // === STEP 4: BTC Cross-Asset Lead (altcoins only) ===
        var scoreBtcLead = 0m;
        var baseAsset = candle.Asset.Symbol.Replace("USDT", "");
        if (baseAsset != "BTC")
        {
            var btcAsset = Asset.Tradeable.FirstOrDefault(a => a.Symbol == "BTCUSDT");
            if (btcAsset is not null)
            {
                var btcResult = _candleBuffer.GetAll(btcAsset, TimeFrame.OneMinute);
                if (btcResult.IsSuccess && btcResult.Value!.Count >= 7)
                {
                    var btc = btcResult.Value!;
                    // BTC OFI delta
                    var btcOfiRecent = (btc[^1].TakerBuyBaseVolume + btc[^2].TakerBuyBaseVolume);
                    var btcVolRecent = (btc[^1].Volume + btc[^2].Volume);
                    var btcOfiR = btcVolRecent > 0 ? btcOfiRecent / btcVolRecent : 0.5m;

                    var btcOfiBase = 0m; var btcVolBase = 0m;
                    for (int i = btc.Count - 7; i < btc.Count - 2; i++)
                    { btcOfiBase += btc[i].TakerBuyBaseVolume; btcVolBase += btc[i].Volume; }
                    var btcOfiBaseR = btcVolBase > 0 ? btcOfiBase / btcVolBase : 0.5m;

                    var btcOfiDelta = btcOfiR - btcOfiBaseR;
                    var altOfiDelta = ofiDelta;
                    var crossDelta = btcOfiDelta - altOfiDelta;
                    scoreBtcLead = Math.Clamp(crossDelta * 10m, -1m, 1m);
                }
            }
        }

        // === STEP 5: VWAP Z-Score Mean Reversion ===
        var vwapSum = 0m; var vwapVolSum = 0m;
        var vwapLookback = Math.Min(60, oneMinCandles.Count);
        for (int i = oneMinCandles.Count - vwapLookback; i < oneMinCandles.Count; i++)
        {
            vwapSum += oneMinCandles[i].Close * oneMinCandles[i].Volume;
            vwapVolSum += oneMinCandles[i].Volume;
        }
        var vwap = vwapVolSum > 0 ? vwapSum / vwapVolSum : oneMinCandles[^1].Close;

        var vwapVarSum = 0m;
        for (int i = oneMinCandles.Count - vwapLookback; i < oneMinCandles.Count; i++)
        {
            var diff = oneMinCandles[i].Close - vwap;
            vwapVarSum += diff * diff * oneMinCandles[i].Volume;
        }
        var vwapStd = vwapVolSum > 0 ? (decimal)Math.Sqrt((double)(vwapVarSum / vwapVolSum)) : 1m;
        var vwapZ = vwapStd > 0.000001m ? (oneMinCandles[^1].Close - vwap) / vwapStd : 0m;
        var scoreVWAP = Math.Clamp(-vwapZ / 2.5m, -1m, 1m); // fade extreme deviations

        // ======================================================
        // COMPOSITE SCORE
        // ======================================================
        const decimal wAutocorr = 0.40m;
        const decimal wOFIDelta = 0.30m;
        const decimal wBtcLead = 0.15m;
        const decimal wVWAP = 0.15m;

        decimal compositeScore;
        if (baseAsset == "BTC")
        {
            // BTC: redistribute BTC lead weight
            compositeScore = (wAutocorr + wBtcLead / 2m) * scoreAutocorr
                           + (wOFIDelta + wBtcLead / 2m) * scoreOFIDelta
                           + wVWAP * scoreVWAP;
        }
        else
        {
            compositeScore = wAutocorr * scoreAutocorr
                           + wOFIDelta * scoreOFIDelta
                           + wBtcLead * scoreBtcLead
                           + wVWAP * scoreVWAP;
        }

        // === MULTI-TIMEFRAME ALIGNMENT FILTER ===
        var currentClose = oneMinCandles[^1].Close;
        var sma5 = oneMinCandles.Skip(oneMinCandles.Count - 5).Average(c => c.Close);
        var sma15 = oneMinCandles.Skip(oneMinCandles.Count - 15).Average(c => c.Close);
        var sma60 = oneMinCandles.Skip(oneMinCandles.Count - 60).Average(c => c.Close);
        var sma240 = oneMinCandles.Count >= 240
            ? oneMinCandles.Skip(oneMinCandles.Count - 240).Average(c => c.Close)
            : sma60;

        var compositeSign = Math.Sign(compositeScore);
        var alignCount = 0;
        if (Math.Sign(currentClose - sma5) == compositeSign) alignCount++;
        if (Math.Sign(currentClose - sma15) == compositeSign) alignCount++;
        if (Math.Sign(currentClose - sma60) == compositeSign) alignCount++;
        if (Math.Sign(currentClose - sma240) == compositeSign) alignCount++;

        // Require 3/4 multi-timeframe alignment
        if (alignCount < 3)
        {
            _logger.LogDebug("{Symbol} MTF misaligned: {Align}/4, score:{Score:F3}", candle.Asset.Symbol, alignCount, compositeScore);
            return;
        }

        // Minimum composite threshold
        if (Math.Abs(compositeScore) < 0.12m)
        {
            _logger.LogDebug("{Symbol} score too weak: {Score:F3}", candle.Asset.Symbol, compositeScore);
            return;
        }

        string direction = compositeScore > 0 ? "Up" : "Down";
        var effectiveDelta = compositeScore / 4.0m;

        _logger.LogInformation(
            "{Symbol} L13 | AC:{AC:F2}(r={R:F3}) OFI:{OFI:F2} BTC:{BTC:F2} VWAP:{VW:F2} | Score:{S:F3} MTF:{MTF}/4 Dir:{D} Prev5m:{P:P3}",
            candle.Asset.Symbol, scoreAutocorr, autocorr, scoreOFIDelta, scoreBtcLead, scoreVWAP,
            compositeScore, alignCount, direction, prev5mReturn);

        // === Market Discovery + Midpoint ===
        var discoverResult = await _discovery.DiscoverMarketsAsync();
        if (discoverResult.IsFailure)
        {
            _logger.LogWarning("Polymarket discovery failed for {Symbol}", candle.Asset.Symbol);
            return;
        }

        var market = discoverResult.Value!
            .FirstOrDefault(m => m.UnderlyingAsset.Equals(baseAsset, StringComparison.OrdinalIgnoreCase)
                              && m.Direction == direction);
        if (market is null)
        {
            _logger.LogDebug("No Polymarket market for {Symbol} {Direction}", candle.Asset.Symbol, direction);
            return;
        }

        var midResult = await _polyClient.GetMidpointAsync(market.RelevantTokenId);
        if (midResult.IsFailure)
        {
            _logger.LogWarning("Polymarket midpoint failed for {Symbol}", candle.Asset.Symbol);
            return;
        }

        var marketPrice = midResult.Value;
        if (direction == "Down")
            marketPrice = 1m - marketPrice;

        var signalDirection = direction == "Up" ? SignalDirection.Up : SignalDirection.Down;

        var signalResult = _signalGenerator.Generate(
            candle.Asset, TimeFrame.FiveMinute, oneMinCandles,
            marketPrice, indicators, signalDirection, effectiveDelta);

        if (signalResult.IsSuccess)
        {
            var sig = signalResult.Value!;
            _logger.LogInformation(
                ">>> SIGNAL: {Symbol}/5m {Direction} | FV:{FV:F3} Market:{Market:F3} Edge:{Edge:F3} Score:{Score:F3}",
                sig.Asset.Symbol, sig.Direction, sig.FairValue, sig.MarketPrice, sig.Edge, compositeScore);

            _publisher.PublishSignalGenerated(sig.ToDto());
            await Task.Delay(2000); // Golden Rule 2: T=0+2s entry
            await DispatchToEnginesAsync(sig);
        }
        else
        {
            _logger.LogDebug("No signal: {Symbol}/5m -- {Reason}", candle.Asset.Symbol, signalResult.Error!.Code);
        }
    }

    private async Task DispatchToEnginesAsync(Domain.Trading.Signal sig)
    {
        var engineTasks = _tradingEngines.Select(async engine =>
        {
            var openResult = await engine.OpenPositionAsync(sig);
            if (openResult.IsFailure)
            {
                _logger.LogDebug(
                    "[{Engine}] OpenPosition skipped: {Reason}",
                    engine.EngineName, openResult.Error!.Code);
                return (engine.EngineName, false, (string?)openResult.Error!.Code, (Guid?)null);
            }
            else if (openResult.Value is not null)
            {
                var trade = openResult.Value;
                _logger.LogInformation(
                    "Trade opened: {Engine} {Symbol} {Direction} size:{Size:F2}",
                    engine.EngineName, sig.Asset.Symbol, sig.Direction, trade.PositionSize);
                _publisher.PublishTradeOpened(trade.ToDto());
                return (engine.EngineName, true, (string?)null, (Guid?)trade.Id);
            }
            else
            {
                return (engine.EngineName, false, (string?)null, (Guid?)null);
            }
        }).ToList();

        var completedTasks = await Task.WhenAll(engineTasks);
        var engineResults = completedTasks
            .Select(r => (engineName: r.Item1, accepted: r.Item2, rejectionCode: r.Item3, tradeId: r.Item4))
            .ToList();

        _ = _tradeLogger.LogSignalWithResultsAsync(sig, engineResults);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await _marketDataProvider.StopStreamAsync(cancellationToken);
        await base.StopAsync(cancellationToken);
    }
}
