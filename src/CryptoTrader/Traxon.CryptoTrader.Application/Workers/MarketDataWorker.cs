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
        if (oneMinCandles.Count < 20) return;

        // ======================================================
        // ENSEMBLE WEIGHTED SCORING ALGORITHM
        // Each feature produces a score in [-1, +1]
        // Positive = UP, Negative = DOWN
        // Weighted sum -> CompositeScore -> Direction + Confidence
        // ======================================================

        // -- SCORE 1: Order Flow Imbalance (OFI) -- weight 0.30
        var takerBuy5 = 0m; var totalVol5 = 0m;
        for (int i = oneMinCandles.Count - 5; i < oneMinCandles.Count; i++)
        {
            takerBuy5 += oneMinCandles[i].TakerBuyBaseVolume;
            totalVol5 += oneMinCandles[i].Volume;
        }
        var ofi5 = totalVol5 > 0 ? (2m * takerBuy5 / totalVol5 - 1m) : 0m;
        var scoreOFI = Math.Clamp(ofi5 * 5m, -1m, 1m);

        // -- SCORE 2: Volume-Weighted Price Change (VWPC) -- weight 0.15
        var vwpcSum = 0m; var volSum = 0m;
        for (int i = oneMinCandles.Count - 5; i < oneMinCandles.Count; i++)
        {
            vwpcSum += (oneMinCandles[i].Close - oneMinCandles[i].Open) * oneMinCandles[i].Volume;
            volSum += oneMinCandles[i].Volume;
        }
        var vwpc = volSum > 0 ? vwpcSum / volSum : 0m;
        var avgPrice = oneMinCandles[^1].Close;
        var vwpcNorm = avgPrice > 0 ? vwpc / avgPrice * 200m : 0m;
        var scoreVWPC = Math.Clamp(vwpcNorm, -1m, 1m);

        // -- SCORE 3: Shadow Imbalance -- weight 0.05
        var shadowSum = 0m;
        for (int i = oneMinCandles.Count - 3; i < oneMinCandles.Count; i++)
        {
            var c = oneMinCandles[i];
            var upper = c.High - Math.Max(c.Open, c.Close);
            var lower = Math.Min(c.Open, c.Close) - c.Low;
            var range = c.High - c.Low;
            shadowSum += range > 0 ? (lower - upper) / range : 0m;
        }
        var scoreShadow = Math.Clamp(shadowSum / 3m * 2m, -1m, 1m);

        // -- SCORE 4: Short Momentum (3-bar return) -- weight 0.10
        var mom3 = oneMinCandles[^4].Close > 0
            ? (oneMinCandles[^1].Close - oneMinCandles[^4].Close) / oneMinCandles[^4].Close
            : 0m;
        var scoreMomentum = Math.Clamp(mom3 * 300m, -1m, 1m);

        // -- SCORE 5: BTC Cross-Asset Lead (altcoins only) -- weight 0.15
        var scoreBtcLead = 0m;
        var baseAsset = candle.Asset.Symbol.Replace("USDT", "");
        if (baseAsset != "BTC")
        {
            var btcAsset = Asset.Tradeable.FirstOrDefault(a => a.Symbol == "BTCUSDT");
            if (btcAsset is not null)
            {
                var btcResult = _candleBuffer.GetAll(btcAsset, TimeFrame.OneMinute);
                if (btcResult.IsSuccess && btcResult.Value!.Count >= 5)
                {
                    var btc = btcResult.Value!;
                    var btcBuy3 = btc[^1].TakerBuyBaseVolume + btc[^2].TakerBuyBaseVolume + btc[^3].TakerBuyBaseVolume;
                    var btcTot3 = btc[^1].Volume + btc[^2].Volume + btc[^3].Volume;
                    var btcOFI = btcTot3 > 0 ? (2m * btcBuy3 / btcTot3 - 1m) : 0m;

                    var altBuy3 = oneMinCandles[^1].TakerBuyBaseVolume + oneMinCandles[^2].TakerBuyBaseVolume + oneMinCandles[^3].TakerBuyBaseVolume;
                    var altTot3 = oneMinCandles[^1].Volume + oneMinCandles[^2].Volume + oneMinCandles[^3].Volume;
                    var altOFI = altTot3 > 0 ? (2m * altBuy3 / altTot3 - 1m) : 0m;

                    var crossLead = btcOFI - altOFI;
                    scoreBtcLead = Math.Clamp(crossLead * 5m, -1m, 1m);
                }
            }
        }

        // -- SCORE 6: Mean Reversion (Z-Score, 30-bar window) -- weight 0.15
        var zScore = ZScoreCalculator.Compute(oneMinCandles);
        var scoreMeanRev = Math.Clamp(-zScore / 2.5m, -1m, 1m);

        // -- SCORE 7: Trade Intensity -- weight 0.10
        var recentTradeCount = 0m;
        for (int i = oneMinCandles.Count - 3; i < oneMinCandles.Count; i++)
            recentTradeCount += oneMinCandles[i].TradeCount;
        recentTradeCount /= 3m;
        var medianTradeCount = 0m;
        var tcLookback = Math.Min(20, oneMinCandles.Count);
        for (int i = oneMinCandles.Count - tcLookback; i < oneMinCandles.Count; i++)
            medianTradeCount += oneMinCandles[i].TradeCount;
        medianTradeCount /= tcLookback;
        var tradeIntensity = medianTradeCount > 0 ? recentTradeCount / medianTradeCount : 1m;
        var scoreTradeIntensity = Math.Clamp((tradeIntensity - 1m) * Math.Sign(scoreOFI) * 2m, -1m, 1m);

        // ======================================================
        // COMPOSITE SCORE -- weighted sum
        // ======================================================
        const decimal wOFI = 0.30m;
        const decimal wVWPC = 0.15m;
        const decimal wShadow = 0.05m;
        const decimal wMomentum = 0.10m;
        const decimal wBtcLead = 0.15m;
        const decimal wMeanRev = 0.15m;
        const decimal wTradeInt = 0.10m;

        decimal compositeScore;
        if (baseAsset == "BTC")
        {
            compositeScore = (wOFI + wBtcLead / 2m) * scoreOFI
                           + wVWPC * scoreVWPC
                           + wShadow * scoreShadow
                           + (wMomentum + wBtcLead / 2m) * scoreMomentum
                           + wMeanRev * scoreMeanRev
                           + wTradeInt * scoreTradeIntensity;
        }
        else
        {
            compositeScore = wOFI * scoreOFI
                           + wVWPC * scoreVWPC
                           + wShadow * scoreShadow
                           + wMomentum * scoreMomentum
                           + wBtcLead * scoreBtcLead
                           + wMeanRev * scoreMeanRev
                           + wTradeInt * scoreTradeIntensity;
        }

        // Agreement bonus
        decimal[] signals = baseAsset == "BTC"
            ? new[] { scoreOFI, scoreVWPC, scoreShadow, scoreMomentum, scoreMeanRev, scoreTradeIntensity }
            : new[] { scoreOFI, scoreVWPC, scoreShadow, scoreMomentum, scoreBtcLead, scoreMeanRev, scoreTradeIntensity };
        var agreeCount = signals.Count(s => Math.Sign(s) == Math.Sign(compositeScore));
        if (agreeCount >= signals.Length - 1)
            compositeScore *= 1.15m;

        // Minimum threshold
        if (Math.Abs(compositeScore) < 0.08m)
        {
            _logger.LogDebug("{Symbol} composite score too weak: {Score:F3}", candle.Asset.Symbol, compositeScore);
            return;
        }

        string direction = compositeScore > 0 ? "Up" : "Down";
        var effectiveDelta = compositeScore / 4.0m;

        _logger.LogInformation(
            "{Symbol} ENSEMBLE | OFI:{OFI:F2} VWPC:{VWPC:F2} Shd:{Shd:F2} Mom:{Mom:F2} BTC:{BTC:F2} MR:{MR:F2} TI:{TI:F2} | Score:{Score:F3} Dir:{Dir}",
            candle.Asset.Symbol, scoreOFI, scoreVWPC, scoreShadow, scoreMomentum, scoreBtcLead, scoreMeanRev, scoreTradeIntensity, compositeScore, direction);

        // -- Market Discovery + Midpoint --
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
