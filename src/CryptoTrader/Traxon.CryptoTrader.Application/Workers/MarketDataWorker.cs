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
    private readonly IFuturesDataProvider        _futuresData;
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
        IFuturesDataProvider futuresData,
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
        _futuresData         = futuresData;
        _logger              = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MarketDataWorker starting — loading historical 1m candles (paginated {Days}-day backfill)...", BackfillDays);

        await BackfillOneMinuteCandlesAsync(stoppingToken);

        _logger.LogInformation("Buffer warm-up complete. Starting futures data streams...");
        await _futuresData.StartAsync(Asset.Tradeable.ToList(), stoppingToken);

        _logger.LogInformation("Starting WebSocket stream (1m only)...");

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

        if (indicatorResult.IsFailure) return;

        var indicators = indicatorResult.Value!;

        await TryGenerateAndDispatchSignalAsync(candle, oneMinCandles, indicators);
    }

    private async Task TryGenerateAndDispatchSignalAsync(
        Candle candle,
        IReadOnlyList<Candle> oneMinCandles,
        Domain.Indicators.TechnicalIndicators indicators)
    {
        if (oneMinCandles.Count < 61) return;

        // ======================================================
        // LOOP 29: RADICAL — Pure price-based multi-timeframe trend
        // All microstructure (OBI, OFI, OBI Mom) REMOVED
        // Thesis: Microstructure valid for seconds, price trend valid for minutes
        // ======================================================

        var baseAsset = candle.Asset.Symbol.Replace("USDT", "");

        // === FEATURE 1: Short-term momentum (5 bar = 5 min) ===
        var mom5Ref = oneMinCandles[^6].Close;
        var mom5Now = oneMinCandles[^1].Close;
        var mom5Change = mom5Ref > 0 ? (mom5Now - mom5Ref) / mom5Ref : 0m;
        var scoreMom5 = Math.Clamp(mom5Change * 200m, -1m, 1m);

        // === FEATURE 2: Medium-term trend (15 bar = 15 min) ===
        var mom15Ref = oneMinCandles[^16].Close;
        var mom15Change = mom15Ref > 0 ? (mom5Now - mom15Ref) / mom15Ref : 0m;
        var scoreMom15 = Math.Clamp(mom15Change * 100m, -1m, 1m);

        // === FEATURE 3: Long-term trend (60 bar = 60 min) ===
        var mom60Ref = oneMinCandles[^61].Close;
        var mom60Change = mom60Ref > 0 ? (mom5Now - mom60Ref) / mom60Ref : 0m;
        var scoreMom60 = Math.Clamp(mom60Change * 50m, -1m, 1m);

        // === VOLATILITY GATE (Parkinson: last 3 vs last 10) ===
        var volShort = 0m;
        for (int i = oneMinCandles.Count - 3; i < oneMinCandles.Count; i++)
        {
            var hl = oneMinCandles[i].High - oneMinCandles[i].Low;
            var mid = (oneMinCandles[i].High + oneMinCandles[i].Low) / 2m;
            if (mid > 0) volShort += (hl / mid) * (hl / mid);
        }
        volShort = (decimal)Math.Sqrt((double)(volShort / 3m));

        var volLong = 0m;
        for (int i = oneMinCandles.Count - 10; i < oneMinCandles.Count; i++)
        {
            var hl = oneMinCandles[i].High - oneMinCandles[i].Low;
            var mid = (oneMinCandles[i].High + oneMinCandles[i].Low) / 2m;
            if (mid > 0) volLong += (hl / mid) * (hl / mid);
        }
        volLong = (decimal)Math.Sqrt((double)(volLong / 10m));
        var volExpansion = volLong > 0 ? volShort / volLong : 1m;

        // === COMPOSITE SCORE (3 price-based features) ===
        // Weights: short-term (5m) dominant, medium/long for trend confirmation
        const decimal wMom5 = 0.45m;
        const decimal wMom15 = 0.30m;
        const decimal wMom60 = 0.25m;
        var compositeScore = wMom5 * scoreMom5 + wMom15 * scoreMom15 + wMom60 * scoreMom60;

        // === Trend Agreement Bonus ===
        // If all 3 timeframes agree on direction, boost; if disagree, discount
        var compSign = Math.Sign(compositeScore);
        var agreeCount = 0;
        if (Math.Sign(scoreMom5) == compSign) agreeCount++;
        if (Math.Sign(scoreMom15) == compSign) agreeCount++;
        if (Math.Sign(scoreMom60) == compSign) agreeCount++;
        if (agreeCount == 3) compositeScore *= 1.20m;  // all agree
        else if (agreeCount <= 1) compositeScore *= 0.60m; // disagree

        // === Funding Rate Contrarian Filter (multiplicative) ===
        var fundingRate = _futuresData.GetFundingRate(candle.Asset.Symbol);
        if (Math.Abs(fundingRate) > 0.0005m) // extreme funding
        {
            if (Math.Sign(fundingRate) != Math.Sign(compositeScore))
                compositeScore *= 1.15m; // contrarian to crowded side = boost
            else
                compositeScore *= 0.85m; // same as crowded side = discount
        }

        // Open Interest change: increasing OI = conviction, decreasing = closing positions
        var oiChange = _futuresData.GetOpenInterestChange(candle.Asset.Symbol);
        if (oiChange > 0.01m)
            compositeScore *= 1.10m; // OI increasing >1% = conviction
        else if (oiChange < -0.01m)
            compositeScore *= 0.90m; // OI decreasing >1% = position closing

        // Volatility gate: expanding vol = boost, contracting = discount
        if (volExpansion > 1.5m)
            compositeScore *= 1.2m; // conviction move
        else if (volExpansion < 0.7m)
            compositeScore *= 0.5m; // flat market, likely noise

        // === VOLUME FILTER (skip dead markets) ===
        var volRecent5 = 0m;
        for (int i = oneMinCandles.Count - 5; i < oneMinCandles.Count; i++)
            volRecent5 += oneMinCandles[i].Volume;
        volRecent5 /= 5m;
        var volAvg20 = 0m;
        for (int i = oneMinCandles.Count - 20; i < oneMinCandles.Count; i++)
            volAvg20 += oneMinCandles[i].Volume;
        volAvg20 /= 20m;
        var volRatio = volAvg20 > 0 ? volRecent5 / volAvg20 : 1m;

        if (volRatio < 0.3m) return;

        // === MINIMUM THRESHOLD ===
        if (Math.Abs(compositeScore) < 0.05m) return;

        string direction = compositeScore > 0 ? "Up" : "Down";
        var effectiveDelta = compositeScore / 3.0m;

        _logger.LogInformation(
            "{Symbol} L29 | Mom5:{M5:F3} Mom15:{M15:F3} Mom60:{M60:F3} Agr:{A} FR:{FR:F6} VolExp:{VE:F2} | Score:{S:F3} Dir:{D}",
            candle.Asset.Symbol, scoreMom5, scoreMom15, scoreMom60, agreeCount, fundingRate, volExpansion, compositeScore, direction);

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
                ">>> SIGNAL: {Symbol}/5m {Direction} | FV:{FV:F3} Market:{Market:F3} Edge:{Edge:F3} Score:{Score:F3}",
                sig.Asset.Symbol, sig.Direction, sig.FairValue, sig.MarketPrice, sig.Edge, compositeScore);

            _publisher.PublishSignalGenerated(sig.ToDto());
            await Task.Delay(2000); // Golden Rule 2: T=0+2s entry
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
        await _futuresData.StopAsync(cancellationToken);
        await _marketDataProvider.StopStreamAsync(cancellationToken);
        await base.StopAsync(cancellationToken);
    }
}
