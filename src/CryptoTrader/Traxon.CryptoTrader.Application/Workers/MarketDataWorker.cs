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
        // Candle OpenTime=:04 closes at :05 → (4+1)%5==0 triggers signal pipeline.
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

        // ══════════════════════════════════════════════════
        // REGIME-BASED PREDICTION ALGORITHM
        // Phase 1: Compute features
        // Phase 2: Classify regime
        // Phase 3: Direction decision
        // ══════════════════════════════════════════════════

        // ── FEATURE 1: True Taker Imbalance (TI) ──
        // Uses REAL Binance TakerBuyBaseVolume
        var takerBuyVol5 = 0m;
        var totalVol5 = 0m;
        for (int i = oneMinCandles.Count - 5; i < oneMinCandles.Count; i++)
        {
            takerBuyVol5 += oneMinCandles[i].TakerBuyBaseVolume;
            totalVol5 += oneMinCandles[i].Volume;
        }
        var takerSellVol5 = totalVol5 - takerBuyVol5;
        var ti = totalVol5 > 0 ? (takerBuyVol5 - takerSellVol5) / totalVol5 : 0m;

        // ── FEATURE 2: Taker Imbalance Acceleration (TIA) ──
        var takerBuyCurrent = 0m; var totalCurrent = 0m;
        var takerBuyPrior = 0m; var totalPrior = 0m;
        for (int i = oneMinCandles.Count - 3; i < oneMinCandles.Count; i++)
        {
            takerBuyCurrent += oneMinCandles[i].TakerBuyBaseVolume;
            totalCurrent += oneMinCandles[i].Volume;
        }
        for (int i = oneMinCandles.Count - 6; i < oneMinCandles.Count - 3; i++)
        {
            takerBuyPrior += oneMinCandles[i].TakerBuyBaseVolume;
            totalPrior += oneMinCandles[i].Volume;
        }
        var tiCurrent = totalCurrent > 0 ? (2m * takerBuyCurrent / totalCurrent - 1m) : 0m;
        var tiPrior = totalPrior > 0 ? (2m * takerBuyPrior / totalPrior - 1m) : 0m;
        var tia = tiCurrent - tiPrior;

        // ── FEATURE 3: Z-Score ──
        var zScore = ZScoreCalculator.Compute(oneMinCandles);

        // ── FEATURE 4: Volume-Price Divergence (VPD) ──
        var priceChange5 = oneMinCandles[^5].Close > 0
            ? (oneMinCandles[^1].Close - oneMinCandles[^5].Close) / oneMinCandles[^5].Close : 0m;
        var recentAvgVol = (oneMinCandles[^1].Volume + oneMinCandles[^2].Volume + oneMinCandles[^3].Volume
                          + oneMinCandles[^4].Volume + oneMinCandles[^5].Volume) / 5m;
        var priorAvgVol = 0m;
        var priorCount = Math.Min(15, oneMinCandles.Count - 5);
        for (int i = oneMinCandles.Count - 5 - priorCount; i < oneMinCandles.Count - 5; i++)
            priorAvgVol += oneMinCandles[i].Volume;
        priorAvgVol = priorCount > 0 ? priorAvgVol / priorCount : recentAvgVol;
        var volChange = priorAvgVol > 0 ? recentAvgVol / priorAvgVol : 1m;

        // ── FEATURE 5: Candle Body Ratio ──
        var bodyRatioSum = 0m;
        for (int i = oneMinCandles.Count - 3; i < oneMinCandles.Count; i++)
        {
            var range = oneMinCandles[i].High - oneMinCandles[i].Low;
            var body = Math.Abs(oneMinCandles[i].Close - oneMinCandles[i].Open);
            bodyRatioSum += range > 0 ? body / range : 0.5m;
        }
        var bodyRatio = bodyRatioSum / 3m;

        // ── FEATURE 6: BTC Cross-Asset Flow (altcoins only) ──
        var crossFlow = 0m;
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
                    var btcTI = btcTot3 > 0 ? (2m * btcBuy3 / btcTot3 - 1m) : 0m;
                    crossFlow = btcTI - tiCurrent;
                }
            }
        }

        // ══════════════════════════════════════════════════
        // PHASE 2: REGIME CLASSIFICATION
        // ══════════════════════════════════════════════════

        string regime;
        decimal directionScore = 0m;
        decimal confidence = 0m;

        var absZ = Math.Abs(zScore);

        // EXTREME Z-SCORE: Always mean-revert (direction agnostic)
        if (absZ > 3.0m)
        {
            regime = "ExtremeMeanRevert";
            directionScore = zScore > 0 ? -4.0m : 4.0m;
            confidence = 0.75m;
        }
        // EXHAUSTION: Price extended + flow reversing + volume dying
        else if (absZ > 1.5m && Math.Sign(tia) != Math.Sign(ti) && Math.Abs(tia) > 0.05m && volChange < 0.8m)
        {
            regime = "Exhaustion";
            directionScore = zScore > 0 ? -3.0m : 3.0m; // Bet on reversal
            confidence = Math.Clamp(Math.Abs(tia) * 4m, 0.40m, 0.80m);
        }
        // MEAN REVERSION: Low vol, indecision candles, Z extended
        else if (absZ > 1.2m && bodyRatio < 0.5m && volChange < 1.3m)
        {
            regime = "MeanReversion";
            directionScore = zScore > 0 ? -2.5m : 2.5m;
            confidence = Math.Clamp(absZ / 3.0m, 0.35m, 0.75m);
            // TI confirmation: if flow supports reversal, boost
            if ((directionScore > 0 && ti > 0.10m) || (directionScore < 0 && ti < -0.10m))
                confidence *= 1.15m;
        }
        // MOMENTUM: Strong flow + conviction candles
        else if (Math.Abs(ti) > 0.10m && bodyRatio > 0.45m)
        {
            regime = "Momentum";
            directionScore = ti > 0 ? 2.5m : -2.5m;
            confidence = Math.Clamp(Math.Abs(ti) * 3m, 0.30m, 0.75m);
            // Volume surge confirmation
            if (volChange > 1.3m)
                confidence *= 1.15m;
            // Cross-asset flow for altcoins
            if (Math.Abs(crossFlow) > 0.08m && Math.Sign(crossFlow) == Math.Sign(directionScore))
                confidence *= 1.10m;
        }
        else
        {
            // No clear regime — skip
            return;
        }

        confidence = Math.Clamp(confidence, 0m, 0.90m);
        if (confidence < 0.30m) return;

        string direction = directionScore > 0 ? "Up" : "Down";
        var effectiveDelta = directionScore / 8.0m;

        // ── Market Discovery + Midpoint ──
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

        _logger.LogInformation(
            "{Symbol} Regime:{Regime} TI:{TI:F3} TIA:{TIA:F3} Z:{Z:F2} VPD:{VPD:F2} BodyR:{BR:F2} Conf:{Conf:F2} Dir:{Dir}",
            candle.Asset.Symbol, regime, ti, tia, zScore, volChange, bodyRatio, confidence, direction);

        var signalResult = _signalGenerator.Generate(
            candle.Asset, TimeFrame.FiveMinute, oneMinCandles,
            marketPrice, indicators, signalDirection, effectiveDelta);

        if (signalResult.IsSuccess)
        {
            var sig = signalResult.Value!;
            _logger.LogInformation(
                ">>> SIGNAL: {Symbol}/5m {Direction} Regime:{Regime} | FV:{FV:F3} Market:{Market:F3} Edge:{Edge:F3} Conf:{Conf:F2}",
                sig.Asset.Symbol, sig.Direction, regime, sig.FairValue, sig.MarketPrice, sig.Edge, confidence);

            _publisher.PublishSignalGenerated(sig.ToDto());
            await Task.Delay(2000);
            await DispatchToEnginesAsync(sig);
        }
        else
        {
            _logger.LogDebug("No signal: {Symbol}/5m — {Reason}", candle.Asset.Symbol, signalResult.Error!.Code);
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
