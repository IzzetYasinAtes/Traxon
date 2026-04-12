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
        // LOOP 33: MATEMATIKSEL — Cont-Kukanov OFI + Permutation Entropy + OU Mean Reversion
        // Academic refs: Cont-Kukanov-Stoikov 2014, Bandt-Pompe 2002, Leung-Li 2015
        // Target: 55-60% WR
        // ======================================================

        var symbol = candle.Asset.Symbol;

        // ===== COMPONENT 1: Cont-Kukanov Multi-Level OFI =====
        var ofi = _futuresData.GetCKOrderFlowImbalance(symbol);
        // Z-score via historical rolling std (simplified: use recent OFI magnitude)
        // For MVP: scale OFI to [-1, 1] using percentile approximation
        var ofiNormalized = Math.Tanh((double)(ofi * 0.01m)); // empirical scaling
        var scoreOFI = (decimal)ofiNormalized;

        // ===== COMPONENT 2: Permutation Entropy Filter =====
        // m=3, look back 20 1-min returns
        var returns = new List<double>();
        for (int i = oneMinCandles.Count - 21; i < oneMinCandles.Count - 1; i++)
        {
            if (oneMinCandles[i].Close > 0)
            {
                var r = (double)((oneMinCandles[i + 1].Close - oneMinCandles[i].Close) / oneMinCandles[i].Close);
                returns.Add(r);
            }
        }

        double permEntropy = ComputePermutationEntropy(returns, m: 3);

        // Gate: only trade if predictability is high (low entropy)
        if (permEntropy >= 0.85)
        {
            _logger.LogDebug("{Symbol} L33 SKIP: PE={PE:F3} (random walk regime)", symbol, permEntropy);
            return;
        }

        // ===== COMPONENT 3: Ornstein-Uhlenbeck Mean Reversion =====
        // Fit OU to last 60 log-prices: dX = θ(μ - X)dt + σ dW
        var logPrices = oneMinCandles.Skip(oneMinCandles.Count - 60)
            .Select(c => (double)Math.Log((double)c.Close)).ToArray();

        var (theta, mu, sigma) = FitOU(logPrices);
        var currentLogPrice = logPrices[^1];
        var tau = 5.0; // 5 minutes ahead

        // Conditional expected log-return
        double expectedReturn = (mu - currentLogPrice) * (1.0 - Math.Exp(-theta * tau));
        double stdDev = sigma * Math.Sqrt((1.0 - Math.Exp(-2.0 * theta * tau)) / (2.0 * theta));

        // P(up) = P(X_{t+5} > X_t) = Φ(expectedReturn / stdDev)
        double pUp = stdDev > 1e-10 ? StandardNormalCDF(expectedReturn / stdDev) : 0.5;
        var scoreOU = (decimal)(pUp - 0.5) * 2m; // [-1, 1]

        // ===== COMPOSITE SCORE =====
        // Weights from literature: OFI strong direct signal, OU mean reversion confirmation
        const decimal wOFI = 0.60m;
        const decimal wOU = 0.40m;
        var composite = wOFI * scoreOFI + wOU * scoreOU;

        // ===== SIGNAL GATES =====
        if (Math.Abs(composite) < 0.15m)
        {
            _logger.LogDebug("{Symbol} L33 SKIP: |composite|={C:F3} below threshold", symbol, Math.Abs(composite));
            return;
        }

        string direction = composite > 0 ? "Up" : "Down";
        var signalDirection = composite > 0 ? SignalDirection.Up : SignalDirection.Down;
        var effectiveDelta = composite / 2m;

        _logger.LogInformation(
            "{Symbol} L33 | OFI:{OFI:F3} OU:{OU:F3} PE:{PE:F3} | C:{C:F3} Dir:{D}",
            symbol, scoreOFI, scoreOU, permEntropy, composite, direction);

        // ===== Market Discovery + Entry =====
        var baseAsset = symbol.Replace("USDT", "");
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

        var signalResult = _signalGenerator.Generate(
            candle.Asset, TimeFrame.FiveMinute, oneMinCandles,
            marketPrice, indicators, signalDirection, effectiveDelta);

        if (signalResult.IsSuccess)
        {
            var sig = signalResult.Value!;
            _logger.LogInformation(
                ">>> SIGNAL: {Symbol}/5m {Direction} | FV:{FV:F3} Market:{Market:F3} Edge:{Edge:F3}",
                sig.Asset.Symbol, sig.Direction, sig.FairValue, sig.MarketPrice, sig.Edge);

            _publisher.PublishSignalGenerated(sig.ToDto());
            await Task.Delay(2000); // T=0+2s entry
            await DispatchToEnginesAsync(sig);
        }
    }

    // ===== HELPER METHODS =====

    private static double ComputePermutationEntropy(List<double> returns, int m = 3)
    {
        if (returns.Count < m) return 1.0;

        var patternCounts = new Dictionary<string, int>();
        for (int i = 0; i <= returns.Count - m; i++)
        {
            var window = returns.Skip(i).Take(m).ToArray();
            var indices = Enumerable.Range(0, m).ToArray();
            Array.Sort(indices, (a, b) => window[a].CompareTo(window[b]));
            var pattern = string.Join(",", indices);
            if (!patternCounts.ContainsKey(pattern)) patternCounts[pattern] = 0;
            patternCounts[pattern]++;
        }

        double total = patternCounts.Values.Sum();
        double entropy = 0;
        foreach (var count in patternCounts.Values)
        {
            var p = count / total;
            if (p > 0) entropy -= p * Math.Log(p);
        }

        // Normalize to [0, 1] by max entropy = log(m!)
        double maxEntropy = Math.Log(Factorial(m));
        return maxEntropy > 0 ? entropy / maxEntropy : 0;
    }

    private static int Factorial(int n) => n <= 1 ? 1 : n * Factorial(n - 1);

    private static (double theta, double mu, double sigma) FitOU(double[] x)
    {
        // Fit dX = theta*(mu - X)dt + sigma*dW via OLS on discrete-time form:
        // X_{t+1} = a + b*X_t + eps, where b = exp(-theta*dt), a = mu*(1-b)
        int n = x.Length - 1;
        if (n < 2) return (0.1, x[^1], 0.01);

        double sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0;
        for (int i = 0; i < n; i++)
        {
            sumX += x[i];
            sumY += x[i + 1];
            sumXY += x[i] * x[i + 1];
            sumX2 += x[i] * x[i];
        }
        double meanX = sumX / n;
        double meanY = sumY / n;
        double b = (sumXY - n * meanX * meanY) / (sumX2 - n * meanX * meanX);
        if (Math.Abs(b) >= 1.0 || double.IsNaN(b)) b = 0.99;
        double a = meanY - b * meanX;
        double theta = -Math.Log(Math.Abs(b));
        double mu = a / (1.0 - b);

        // Estimate sigma from residuals
        double sumRes2 = 0;
        for (int i = 0; i < n; i++)
        {
            var pred = a + b * x[i];
            sumRes2 += (x[i + 1] - pred) * (x[i + 1] - pred);
        }
        double sigma = Math.Sqrt(sumRes2 / n);

        return (theta, mu, sigma);
    }

    private static double StandardNormalCDF(double x)
    {
        // Abramowitz-Stegun approximation
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
