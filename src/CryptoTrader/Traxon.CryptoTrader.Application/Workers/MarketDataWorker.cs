using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Traxon.CryptoTrader.Application.Abstractions;
using Traxon.CryptoTrader.Application.DTOs;
using Traxon.CryptoTrader.Application.Mappings;
using Traxon.CryptoTrader.Domain.Assets;
using Traxon.CryptoTrader.Domain.Market;

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
    private const int MinFiveMinuteCandles = 30;

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
        AggregateBackfilledCandles();

        _logger.LogInformation("Buffer warm-up complete. Starting WebSocket stream (1m only)...");

        var engineCount = _tradingEngines.Count();
        _publisher.PublishSystemStatus(new SystemStatusDto(
            IsRunning: true,
            IsBinanceConnected: true,
            ActiveEngineCount: engineCount,
            StartedAt: DateTime.UtcNow));

        await _marketDataProvider.StartStreamAsync(
            assets: Asset.Tradeable,
            timeFrames: TimeFrame.Streamed,
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

    private void AggregateBackfilledCandles()
    {
        foreach (var asset in Asset.Tradeable)
        {
            var allOneMinute = _candleBuffer.GetAll(asset, TimeFrame.OneMinute);
            if (allOneMinute.IsFailure) continue;

            foreach (var targetTf in TimeFrame.Aggregated)
            {
                var aggregated = CandleAggregator.AggregateAll(allOneMinute.Value!, asset, targetTf);
                foreach (var candle in aggregated)
                    _candleBuffer.Add(candle);

                _logger.LogInformation("Aggregated {Count} {TF} candles for {Symbol}",
                    aggregated.Count, targetTf.Value, asset.Symbol);
            }
        }
    }

    private async Task OnCandleClosedAsync(Candle candle)
    {
        _candleBuffer.Add(candle);
        PublishTickerUpdate(candle);
        _publisher.PublishCandleUpdate(candle.ToCandleDto());
        WriteCandleAsync(candle);

        // Aggregate 1m candle into higher timeframes at boundaries
        foreach (var targetTf in TimeFrame.Aggregated)
        {
            if (!CandleAggregator.CompletesTimeFrame(candle, targetTf))
                continue;

            var minutesNeeded = targetTf.TotalSeconds / 60;
            var recentResult = _candleBuffer.GetLast(candle.Asset, TimeFrame.OneMinute, minutesNeeded);
            if (recentResult.IsFailure) continue;

            var aggregated = CandleAggregator.Aggregate(recentResult.Value!, candle.Asset, targetTf);
            if (aggregated is null) continue;

            _candleBuffer.Add(aggregated);
            WriteCandleAsync(aggregated);

            if (targetTf == TimeFrame.FiveMinute)
                await RunSignalPipelineAsync(aggregated);
        }

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

    private async Task RunSignalPipelineAsync(Candle fiveMinCandle)
    {
        if (!_candleBuffer.IsWarmedUp(fiveMinCandle.Asset, TimeFrame.FiveMinute, minimumCandles: MinFiveMinuteCandles))
        {
            _logger.LogDebug("Buffer not warmed up yet for {Symbol}/5m", fiveMinCandle.Asset.Symbol);
            return;
        }

        var candlesResult = _candleBuffer.GetAll(fiveMinCandle.Asset, TimeFrame.FiveMinute);
        if (candlesResult.IsFailure) return;

        var indicatorResult = _indicatorCalculator.Calculate(
            fiveMinCandle.Asset, TimeFrame.FiveMinute, candlesResult.Value!);

        if (indicatorResult.IsFailure)
        {
            _logger.LogWarning("Indicator calc failed for {Symbol}/5m: {Error}",
                fiveMinCandle.Asset.Symbol, indicatorResult.Error!.Message);
            return;
        }

        var indicators = indicatorResult.Value!;
        _logger.LogInformation(
            "{Symbol}/5m — RSI:{Rsi:F1} MACD:{Macd:F6} BB({Lower:F2}/{Upper:F2}) ATR:{Atr:F6} Bulls:{Bulls}/5",
            fiveMinCandle.Asset.Symbol,
            indicators.Rsi.Value,
            indicators.Macd.Histogram,
            indicators.BollingerBands.Lower, indicators.BollingerBands.Upper,
            indicators.Atr.Value,
            indicators.BullishCount());

        await TryGenerateAndDispatchSignalAsync(fiveMinCandle, candlesResult.Value!, indicators);
    }

    private async Task TryGenerateAndDispatchSignalAsync(
        Candle candle,
        IReadOnlyList<Candle> fiveMinCandles,
        Domain.Indicators.TechnicalIndicators indicators)
    {
        // Compute Z-score from 1m candles to determine preliminary direction (mean reversion)
        var oneMinuteResult = _candleBuffer.GetAll(candle.Asset, TimeFrame.OneMinute);
        if (oneMinuteResult.IsFailure) return;

        var oneMinuteCandles = oneMinuteResult.Value!;
        var zScore = ZScoreCalculator.Compute(oneMinuteCandles);

        // Mean reversion: positive Z = overbought = bet Down; negative Z = oversold = bet Up
        var direction = zScore > 0 ? "Down" : "Up";

        var baseAsset = candle.Asset.Symbol.Replace("USDT", "");

        var discoverResult = await _discovery.DiscoverMarketsAsync();
        if (discoverResult.IsFailure)
        {
            _logger.LogWarning("Polymarket market discovery failed for {Symbol}, skipping signal", candle.Asset.Symbol);
            return;
        }

        var market = discoverResult.Value!
            .FirstOrDefault(m => m.UnderlyingAsset.Equals(baseAsset, StringComparison.OrdinalIgnoreCase)
                              && m.Direction == direction);
        if (market is null)
        {
            _logger.LogDebug("No Polymarket market for {Symbol} {Direction}, skipping signal", candle.Asset.Symbol, direction);
            return;
        }

        var midResult = await _polyClient.GetMidpointAsync(market.RelevantTokenId);
        if (midResult.IsFailure)
        {
            _logger.LogWarning("Polymarket midpoint failed for {Symbol}, skipping signal", candle.Asset.Symbol);
            return;
        }

        var marketPrice = midResult.Value;

        // FairValue = P(Up). Down token midpoint = P(Down). Convert to same basis.
        if (direction == "Down")
            marketPrice = 1m - marketPrice;

        var signalResult = _signalGenerator.Generate(
            candle.Asset, candle.TimeFrame, oneMinuteCandles,
            marketPrice, indicators);

        if (signalResult.IsSuccess)
        {
            var sig = signalResult.Value!;
            _logger.LogInformation(
                ">>> SIGNAL: {Symbol}/5m {Direction} | FV:{FV:F3} Market:{Market:F3} Edge:{Edge:F3} " +
                "Kelly:{Kelly:F4} Regime:{Regime} Bulls:{Bulls}/5",
                sig.Asset.Symbol, sig.Direction,
                sig.FairValue, sig.MarketPrice, sig.Edge, sig.KellyFraction, sig.Regime,
                sig.Indicators.BullishCount());

            _publisher.PublishSignalGenerated(sig.ToDto());
            await DispatchToEnginesAsync(sig);
        }
        else
        {
            _logger.LogDebug("No signal: {Symbol}/5m — {Reason}",
                candle.Asset.Symbol, signalResult.Error!.Code);
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
