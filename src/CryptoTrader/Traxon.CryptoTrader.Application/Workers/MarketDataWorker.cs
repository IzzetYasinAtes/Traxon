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

        await TryGenerateAndDispatchSignalAsync(candle, oneMinCandles, indicators);
    }

    private async Task TryGenerateAndDispatchSignalAsync(
        Candle candle,
        IReadOnlyList<Candle> oneMinCandles,
        Domain.Indicators.TechnicalIndicators indicators)
    {
        if (oneMinCandles.Count < 250) return;

        // ======================================================
        // LOOP 14: AUTOCORRELATION + OFI DELTA + CONVERGENCE
        //          + RSI MULTIPLIER + ADAPTIVE THRESHOLD
        //          + VOLUME CONFIRMATION + DOJI FILTER
        // ======================================================

        var baseAsset = candle.Asset.Symbol.Replace("USDT", "");

        // === STEP 1: EWMA Autocorrelation (lambda=0.90, 50-window) ===
        var fiveMinReturns = new List<decimal>();
        for (int end = oneMinCandles.Count - 5; end >= 5; end -= 5)
        {
            var open = oneMinCandles[end].Open;
            var close = oneMinCandles[end + 4].Close;
            if (open > 0)
                fiveMinReturns.Add((close - open) / open);
            if (fiveMinReturns.Count >= 50) break;
        }
        fiveMinReturns.Reverse();

        var autocorr = 0m;
        if (fiveMinReturns.Count >= 15)
        {
            var n = fiveMinReturns.Count - 1;
            var meanR = fiveMinReturns.Take(n).Average();
            var meanR1 = fiveMinReturns.Skip(1).Average();
            decimal ewmaCov = 0, ewmaVar0 = 0, ewmaVar1 = 0;
            const decimal lambda = 0.90m;
            for (int i = 0; i < n; i++)
            {
                var weight = (decimal)Math.Pow((double)lambda, n - 1 - i);
                var r0 = fiveMinReturns[i] - meanR;
                var r1 = fiveMinReturns[i + 1] - meanR1;
                ewmaCov += weight * r0 * r1;
                ewmaVar0 += weight * r0 * r0;
                ewmaVar1 += weight * r1 * r1;
            }
            var denom = (decimal)Math.Sqrt((double)(ewmaVar0 * ewmaVar1));
            autocorr = denom > 0 ? ewmaCov / denom : 0m;
        }

        // Per-asset minimum autocorrelation gate
        var minAutocorr = (baseAsset == "SOL" || baseAsset == "ETH") ? 0.08m : 0.03m;

        var prev5mReturn = oneMinCandles[^5].Open > 0
            ? (oneMinCandles[^1].Close - oneMinCandles[^5].Open) / oneMinCandles[^5].Open
            : 0m;

        decimal scoreAutocorr;
        if (Math.Abs(autocorr) < minAutocorr)
        {
            scoreAutocorr = 0m;
        }
        else if (autocorr < 0)
        {
            scoreAutocorr = -Math.Sign(prev5mReturn) * Math.Clamp(Math.Abs(prev5mReturn) * 200m, 0m, 1m);
        }
        else
        {
            scoreAutocorr = Math.Sign(prev5mReturn) * Math.Clamp(Math.Abs(prev5mReturn) * 200m, 0m, 1m);
        }

        if (Math.Abs(prev5mReturn) < 0.0010m)
            scoreAutocorr *= 0.3m;

        // === STEP 2: Volume confirmation for mean reversion ===
        var volRecent5 = 0m;
        for (int i = oneMinCandles.Count - 5; i < oneMinCandles.Count; i++)
            volRecent5 += oneMinCandles[i].Volume;
        volRecent5 /= 5m;

        var volAvg20 = 0m;
        for (int i = oneMinCandles.Count - 20; i < oneMinCandles.Count; i++)
            volAvg20 += oneMinCandles[i].Volume;
        volAvg20 /= 20m;

        var volRatio = volAvg20 > 0 ? volRecent5 / volAvg20 : 1m;

        if (autocorr < 0) // mean reversion regime
        {
            if (volRatio > 2.0m)
                scoreAutocorr *= 1.3m; // exhaustion confirmed
            else if (volRatio < 0.8m)
                scoreAutocorr *= 0.6m; // low vol = weak setup
        }

        // === STEP 3: OFI Delta (time-weighted) ===
        var w1 = 0.67m; var w2 = 0.33m;
        var ofiRecent = w1 * oneMinCandles[^1].TakerBuyBaseVolume + w2 * oneMinCandles[^2].TakerBuyBaseVolume;
        var volRecentOfi = w1 * oneMinCandles[^1].Volume + w2 * oneMinCandles[^2].Volume;
        var ofiRecentRatio = volRecentOfi > 0 ? ofiRecent / volRecentOfi : 0.5m;

        var ofiBaseline = 0m; var volBaseline = 0m;
        for (int i = 0; i < 5; i++)
        {
            var w = (decimal)Math.Pow(0.8, i);
            var idx = oneMinCandles.Count - 3 - i;
            ofiBaseline += w * oneMinCandles[idx].TakerBuyBaseVolume;
            volBaseline += w * oneMinCandles[idx].Volume;
        }
        var ofiBaselineRatio = volBaseline > 0 ? ofiBaseline / volBaseline : 0.5m;

        var ofiDelta = ofiRecentRatio - ofiBaselineRatio;
        var scoreOFIDelta = Math.Clamp(ofiDelta * 15m, -1m, 1m);

        // === STEP 4: BTC Cross-Asset Lead (per-asset lag, disabled for ETH) ===
        var scoreBtcLead = 0m;
        if (baseAsset != "BTC" && baseAsset != "ETH") // ETH too correlated, BTC lead useless
        {
            int btcLag = baseAsset switch { "DOGE" => 2, _ => 1 };
            var btcAsset = Asset.Tradeable.FirstOrDefault(a => a.Symbol == "BTCUSDT");
            if (btcAsset is not null)
            {
                var btcResult = _candleBuffer.GetAll(btcAsset, TimeFrame.OneMinute);
                if (btcResult.IsSuccess && btcResult.Value!.Count >= 7 + btcLag)
                {
                    var btc = btcResult.Value!;
                    var btcOfiR = (btc[^(1 + btcLag)].TakerBuyBaseVolume + btc[^(2 + btcLag)].TakerBuyBaseVolume);
                    var btcVolR = (btc[^(1 + btcLag)].Volume + btc[^(2 + btcLag)].Volume);
                    var btcRatio = btcVolR > 0 ? btcOfiR / btcVolR : 0.5m;

                    var btcOfiB = 0m; var btcVolB = 0m;
                    for (int i = 0; i < 5; i++)
                    {
                        var idx = btc.Count - 3 - btcLag - i;
                        if (idx >= 0) { btcOfiB += btc[idx].TakerBuyBaseVolume; btcVolB += btc[idx].Volume; }
                    }
                    var btcBaseR = btcVolB > 0 ? btcOfiB / btcVolB : 0.5m;

                    var crossDelta = (btcRatio - btcBaseR) - ofiDelta;
                    scoreBtcLead = Math.Clamp(crossDelta * 10m, -1m, 1m);
                }
            }
        }

        // === STEP 5: VWAP Z-Score ===
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
        var scoreVWAP = Math.Clamp(-vwapZ / 2.5m, -1m, 1m);

        // ======================================================
        // COMPOSITE SCORE
        // ======================================================
        const decimal wAutocorr = 0.40m;
        const decimal wOFIDelta = 0.30m;
        const decimal wBtcLead = 0.15m;
        const decimal wVWAP = 0.15m;

        decimal compositeScore;
        if (baseAsset == "BTC" || baseAsset == "ETH")
        {
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

        // === RSI(14) CONTINUOUS MULTIPLIER ===
        var rsi = indicators.Rsi.Value;
        if (compositeScore > 0) // UP signal
        {
            if (rsi < 25) compositeScore *= 1.25m;      // RSI oversold confirms UP
            else if (rsi > 35 && rsi < 65) compositeScore *= 0.85m; // neutral RSI = weak
        }
        else if (compositeScore < 0) // DOWN signal
        {
            if (rsi > 75) compositeScore *= 1.25m;      // RSI overbought confirms DOWN
            else if (rsi > 35 && rsi < 65) compositeScore *= 0.85m; // neutral RSI = weak
        }

        // === DOJI FILTER (last candle indecision) ===
        var lastCandle = oneMinCandles[^1];
        var bodyRatio = (lastCandle.High - lastCandle.Low) > 0
            ? Math.Abs(lastCandle.Close - lastCandle.Open) / (lastCandle.High - lastCandle.Low)
            : 0m;
        if (bodyRatio < 0.20m)
            compositeScore *= 0.5m; // doji = indecision, heavy discount

        // Strong candle confirmation
        if (bodyRatio > 0.60m)
        {
            bool isBullish = lastCandle.Close > lastCandle.Open;
            bool confirms = (compositeScore > 0 && isBullish) || (compositeScore < 0 && !isBullish);
            compositeScore *= confirms ? 1.15m : 0.75m;
        }

        // === CONVERGENCE SCORING (AC + RSI + BB + VWAP agree) ===
        int convergence = 0;
        if (autocorr < -0.05m && Math.Abs(prev5mReturn) > 0.0015m) convergence++;
        if ((compositeScore > 0 && rsi < 30) || (compositeScore < 0 && rsi > 70)) convergence++;

        var bbRange = indicators.BollingerBands.Upper - indicators.BollingerBands.Lower;
        var bbPos = bbRange > 0
            ? (oneMinCandles[^1].Close - indicators.BollingerBands.Lower) / bbRange
            : 0.5m;
        if ((compositeScore > 0 && bbPos < 0.10m) || (compositeScore < 0 && bbPos > 0.90m)) convergence++;
        if (Math.Abs(vwapZ) > 1.5m) convergence++;

        var convergenceMultiplier = convergence switch
        {
            0 => 0.60m,
            1 => 0.85m,
            2 => 1.0m,
            3 => 1.30m,
            _ => 1.50m
        };
        compositeScore *= convergenceMultiplier;

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

        if (alignCount < 3)
        {
            _logger.LogDebug("{Symbol} MTF misaligned: {Align}/4", candle.Asset.Symbol, alignCount);
            return;
        }

        // === ADAPTIVE THRESHOLD PER ASSET ===
        var atrPct = indicators.Atr.Value / oneMinCandles[^1].Close;
        var adaptiveThreshold = Math.Clamp(0.08m + atrPct * 30m, 0.08m, 0.25m);

        if (Math.Abs(compositeScore) < adaptiveThreshold)
        {
            _logger.LogDebug("{Symbol} below adaptive threshold: {Score:F3} < {Thresh:F3}",
                candle.Asset.Symbol, Math.Abs(compositeScore), adaptiveThreshold);
            return;
        }

        string direction = compositeScore > 0 ? "Up" : "Down";
        var effectiveDelta = compositeScore / 4.0m;

        _logger.LogInformation(
            "{Symbol} L14 | AC:{AC:F2}(r={R:F3}) OFI:{OFI:F2} BTC:{BTC:F2} VW:{VW:F2} RSI:{RSI:F0} Conv:{CV} MTF:{MTF}/4 | Score:{S:F3} Thr:{T:F3} Dir:{D}",
            candle.Asset.Symbol, scoreAutocorr, autocorr, scoreOFIDelta, scoreBtcLead, scoreVWAP,
            rsi, convergence, alignCount, compositeScore, adaptiveThreshold, direction);

        // === Market Discovery + Entry ===
        var discoverResult = await _discovery.DiscoverMarketsAsync();
        if (discoverResult.IsFailure) return;

        var market = discoverResult.Value!
            .FirstOrDefault(m => m.UnderlyingAsset.Equals(baseAsset, StringComparison.OrdinalIgnoreCase)
                              && m.Direction == direction);
        if (market is null) return;

        var midResult = await _polyClient.GetMidpointAsync(market.RelevantTokenId);
        if (midResult.IsFailure) return;

        var marketPrice = midResult.Value;
        if (direction == "Down") marketPrice = 1m - marketPrice;

        var signalDirection = direction == "Up" ? SignalDirection.Up : SignalDirection.Down;

        var signalResult = _signalGenerator.Generate(
            candle.Asset, TimeFrame.FiveMinute, oneMinCandles,
            marketPrice, indicators, signalDirection, effectiveDelta);

        if (signalResult.IsSuccess)
        {
            var sig = signalResult.Value!;
            _logger.LogInformation(
                ">>> SIGNAL: {Symbol}/5m {Direction} | FV:{FV:F3} Market:{Market:F3} Edge:{Edge:F3} Score:{Score:F3} Conv:{Conv}",
                sig.Asset.Symbol, sig.Direction, sig.FairValue, sig.MarketPrice, sig.Edge, compositeScore, convergence);

            _publisher.PublishSignalGenerated(sig.ToDto());
            await Task.Delay(2000);
            await DispatchToEnginesAsync(sig);
        }
    }

    private async Task DispatchToEnginesAsync(Domain.Trading.Signal sig)
    {
        var engineTasks = _tradingEngines.Select(async engine =>
        {
            var openResult = await engine.OpenPositionAsync(sig);
            if (openResult.IsFailure)
            {
                _logger.LogDebug("[{Engine}] OpenPosition skipped: {Reason}",
                    engine.EngineName, openResult.Error!.Code);
                return (engine.EngineName, false, (string?)openResult.Error!.Code, (Guid?)null);
            }
            else if (openResult.Value is not null)
            {
                var trade = openResult.Value;
                _logger.LogInformation("Trade opened: {Engine} {Symbol} {Direction} size:{Size:F2}",
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
