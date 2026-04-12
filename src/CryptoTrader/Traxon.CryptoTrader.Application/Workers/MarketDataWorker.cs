using System.Collections.Concurrent;
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

    // BTC lead-lag tracking — last 6 BTC closes for 5-min momentum
    private static readonly ConcurrentQueue<(DateTime time, decimal close)> _btcCloses = new();
    private const int BtcCloseHistory = 10;

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

        // Track BTC closes for lead-lag feature
        if (candle.Asset.Symbol == "BTCUSDT")
        {
            _btcCloses.Enqueue((candle.OpenTime, candle.Close));
            while (_btcCloses.Count > BtcCloseHistory)
                _btcCloses.TryDequeue(out _);
        }

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
        if (oneMinCandles.Count < 60) return;

        // ======================================================
        // LOOP 35: Loop34 base + EWMA volatility (λ=0.94) + drift term + DOWN midpoint fix
        // Benjamin-Cup (Feb 2026): Brownian motion implied prob vs Polymarket mid
        // Academic refs: Black-Scholes 1973, Reiner-Rubinstein 1991, RiskMetrics 1996
        // Target: 5-10 signals/hour, 62-68% hit rate, 3¢+ edge
        // ======================================================

        var symbol = candle.Asset.Symbol;
        var baseAsset = symbol.Replace("USDT", "");

        // === STEP 1: Find window opening price ===
        // The 5-min window opening bar (OpenTime where minute % 5 == 0)
        // candle.OpenTime is the just-closed 1m candle (minute is :04, :09, :14, etc.)
        // The 5-min window opening bar is candle.OpenTime - 4 minutes (or the :00, :05, :10, :15... bar)
        var windowOpenMinute = (candle.OpenTime.Minute / 5) * 5;
        var windowOpenTime = new DateTime(candle.OpenTime.Year, candle.OpenTime.Month, candle.OpenTime.Day,
                                          candle.OpenTime.Hour, windowOpenMinute, 0, DateTimeKind.Utc);

        var windowOpenCandle = oneMinCandles.FirstOrDefault(c => c.OpenTime == windowOpenTime);
        if (windowOpenCandle == null) return;

        var spotStart = windowOpenCandle.Open; // price at window start
        var spotNow = candle.Close; // latest price (just closed candle)

        // === STEP 2: Compute realized volatility (EWMA, RiskMetrics λ=0.94) ===
        var returns = new List<double>();
        for (int i = oneMinCandles.Count - 61; i < oneMinCandles.Count - 1; i++)
        {
            if (i < 0) continue;
            if (oneMinCandles[i].Close > 0)
            {
                var r = Math.Log((double)(oneMinCandles[i + 1].Close / oneMinCandles[i].Close));
                returns.Add(r);
            }
        }
        if (returns.Count < 30) return;

        // EWMA variance (λ=0.94) — regime change'e hızlı adapte
        const double lambda = 0.94;
        double sigmaSquared = returns[0] * returns[0]; // seed with first squared return
        for (int i = 1; i < returns.Count; i++)
        {
            sigmaSquared = lambda * sigmaSquared + (1.0 - lambda) * returns[i] * returns[i];
        }
        double sigmaPerMin = Math.Sqrt(sigmaSquared);

        if (sigmaPerMin < 1e-6) return; // no volatility, skip

        // === STEP 3: Brownian implied probability (with drift) ===
        double tau = 5.0 - 2.0 / 60.0; // 5 min - 2 sec entry delay ≈ 4.967 min
        double lnRatio = Math.Log((double)(spotNow / spotStart));

        // Drift: mean of last 15 1-min log returns (per minute trend bias)
        int driftWindow = Math.Min(15, returns.Count);
        double mu = 0.0;
        for (int i = returns.Count - driftWindow; i < returns.Count; i++) mu += returns[i];
        mu /= driftWindow;

        // Full Brownian with drift: z = (ln(S_t/S_0) + (μ - 0.5σ²)·τ) / (σ·√τ)
        double z = (lnRatio + (mu - 0.5 * sigmaPerMin * sigmaPerMin) * tau)
                 / (sigmaPerMin * Math.Sqrt(tau));
        decimal impliedProbUp = (decimal)StandardNormalCDF(z);

        // === STEP 4: Fetch Polymarket UP midpoint ===
        var discoverResult = await _discovery.DiscoverMarketsAsync();
        if (discoverResult.IsFailure) return;

        var marketUp = discoverResult.Value!
            .FirstOrDefault(m => m.UnderlyingAsset.Equals(baseAsset, StringComparison.OrdinalIgnoreCase)
                              && m.Direction == "Up");
        var marketDown = discoverResult.Value!
            .FirstOrDefault(m => m.UnderlyingAsset.Equals(baseAsset, StringComparison.OrdinalIgnoreCase)
                              && m.Direction == "Down");

        if (marketUp is null || marketDown is null) return;

        var midUpResult = await _polyClient.GetMidpointAsync(marketUp.RelevantTokenId);
        if (midUpResult.IsFailure) return;
        var polyMidUp = midUpResult.Value;

        // === STEP 5: Compute edge ===
        var edge = impliedProbUp - polyMidUp;
        var absEdge = Math.Abs(edge);

        _logger.LogInformation(
            "{Symbol} L35 | spot0:{S0:F2} spotNow:{SN:F2} σewma:{Sig:F5} μ:{Mu:F6} τ:{Tau:F2} Φ(z):{Prob:F3} polyUp:{Poly:F3} edge:{Edge:F3}",
            symbol, spotStart, spotNow, sigmaPerMin, mu, tau, impliedProbUp, polyMidUp, edge);

        if (absEdge < 0.03m)
        {
            _logger.LogDebug("{Symbol} L35 SKIP: edge {Edge:F3} below 0.03 threshold", symbol, edge);
            return;
        }

        // === STEP 6: Determine direction ===
        string direction;
        decimal marketPrice;
        Traxon.CryptoTrader.Application.Polymarket.Models.PolymarketMarket market;
        SignalDirection signalDirection;

        if (edge > 0)
        {
            // impliedProb > polyMid → UP is underpriced → buy UP
            direction = "Up";
            signalDirection = SignalDirection.Up;
            market = marketUp;
            marketPrice = polyMidUp;
        }
        else
        {
            // UP overpriced → DOWN underpriced → fetch DOWN midpoint separately
            direction = "Down";
            signalDirection = SignalDirection.Down;
            market = marketDown;

            var midDownResult = await _polyClient.GetMidpointAsync(marketDown.RelevantTokenId);
            if (midDownResult.IsFailure) return;
            marketPrice = midDownResult.Value;
        }

        // === STEP 7: Build signal with edge as conviction ===
        var effectiveDelta = edge; // direct, signed, capped by natural Brownian range

        _logger.LogInformation(
            ">>> SIGNAL: {Symbol}/5m {Direction} | BrownianProb:{BP:F3} PolyMid:{PM:F3} Edge:{E:F3}",
            symbol, direction, impliedProbUp, polyMidUp, edge);

        var signalResult = _signalGenerator.Generate(
            candle.Asset, TimeFrame.FiveMinute, oneMinCandles,
            marketPrice, indicators, signalDirection, effectiveDelta);

        if (signalResult.IsSuccess)
        {
            var sig = signalResult.Value!;
            _logger.LogInformation(
                ">>> DISPATCH: {Symbol}/5m {Direction} FV:{FV:F3} Market:{Market:F3} Edge:{Edge:F3}",
                sig.Asset.Symbol, sig.Direction, sig.FairValue, sig.MarketPrice, sig.Edge);

            _publisher.PublishSignalGenerated(sig.ToDto());
            await Task.Delay(2000); // T=0+2s entry
            await DispatchToEnginesAsync(sig);
        }
    }

    // === Helper: Standard Normal CDF (Abramowitz-Stegun approximation) ===
    private static double StandardNormalCDF(double x)
    {
        double t = 1.0 / (1.0 + 0.2316419 * Math.Abs(x));
        double d = 0.3989422804 * Math.Exp(-x * x / 2.0);
        double p = d * t * (0.31938153 + t * (-0.356563782 + t * (1.781477937 + t * (-1.821255978 + t * 1.330274429))));
        return x >= 0 ? 1.0 - p : p;
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
