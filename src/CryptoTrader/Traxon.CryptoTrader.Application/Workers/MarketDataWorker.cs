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
        if (oneMinCandles.Count < 15) return;

        // ══════════════════════════════════════════════════
        // MULTI-SIGNAL WEIGHTED SCORE ALGORITHM
        // Each signal contributes to a direction score.
        // Positive = UP, Negative = DOWN.
        // ══════════════════════════════════════════════════

        var directionScore = 0m;
        var signalCount = 0;

        // ── SIGNAL 1: BTC Lead-Lag (for altcoins only) ──
        // BTC moves first, altcoins follow with 1-5 min lag.
        // If BTC moved but this asset hasn't → bet this asset follows BTC.
        var baseAsset = candle.Asset.Symbol.Replace("USDT", "");
        if (baseAsset != "BTC")
        {
            var btcAsset = Asset.Tradeable.FirstOrDefault(a => a.Symbol == "BTCUSDT");
            if (btcAsset is not null)
            {
                var btcResult = _candleBuffer.GetAll(btcAsset, TimeFrame.OneMinute);
                if (btcResult.IsSuccess && btcResult.Value!.Count >= 5)
                {
                    var btcCandles = btcResult.Value!;
                    var btcReturn2m = (btcCandles[^1].Close - btcCandles[^3].Close) / btcCandles[^3].Close;
                    var assetReturn2m = (oneMinCandles[^1].Close - oneMinCandles[^3].Close) / oneMinCandles[^3].Close;
                    var lagSignal = btcReturn2m - assetReturn2m;

                    if (Math.Abs(lagSignal) > 0.0008m) // 8 bps divergence
                    {
                        directionScore += lagSignal > 0 ? 3.0m : -3.0m;
                        signalCount++;
                    }
                }
            }
        }

        // ── SIGNAL 2: Volume Surge + Direction ──
        // High volume in recent candles vs prior = conviction.
        if (oneMinCandles.Count >= 10)
        {
            var recentVol = (oneMinCandles[^1].Volume + oneMinCandles[^2].Volume + oneMinCandles[^3].Volume) / 3m;
            var priorVol = 0m;
            for (int i = oneMinCandles.Count - 10; i < oneMinCandles.Count - 3; i++)
                priorVol += oneMinCandles[i].Volume;
            priorVol /= 7m;

            if (priorVol > 0)
            {
                var volRatio = recentVol / priorVol;
                if (volRatio > 1.3m)
                {
                    var recentDir = oneMinCandles[^1].Close > oneMinCandles[^3].Close ? 1m : -1m;
                    directionScore += recentDir * 2.0m;
                    signalCount++;
                }
            }
        }

        // ── SIGNAL 3: Micro-Momentum (last 2 candles) ──
        {
            var mom1 = oneMinCandles[^1].Close - oneMinCandles[^1].Open;
            var mom2 = oneMinCandles[^2].Close - oneMinCandles[^2].Open;
            if (Math.Sign(mom1) == Math.Sign(mom2) && mom1 != 0 && mom2 != 0)
            {
                var dir = mom1 > 0 ? 1m : -1m;
                directionScore += dir * 1.5m;
                signalCount++;

                // Acceleration bonus
                if (Math.Abs(mom1) > Math.Abs(mom2))
                {
                    directionScore += dir * 1.0m;
                }
            }
        }

        // ── SIGNAL 4: Previous Window Delta ──
        if (oneMinCandles.Count >= 6)
        {
            var windowDelta = (oneMinCandles[^1].Close - oneMinCandles[^5].Open) / oneMinCandles[^5].Open;
            if (Math.Abs(windowDelta) > 0.0003m)
            {
                directionScore += windowDelta > 0 ? 1.5m : -1.5m;
                signalCount++;
            }
        }

        // ── SIGNAL 5: Z-Score Mean Reversion Override ──
        // At extreme Z-scores, momentum reverses.
        var zScore = ZScoreCalculator.Compute(oneMinCandles);
        if (Math.Abs(zScore) > 2.5m)
        {
            // Strong reversion: override other signals
            directionScore = zScore > 0 ? -3.0m : 3.0m;
            signalCount = 1; // Reset — this overrides
        }

        // ── DECISION ──
        if (signalCount == 0) return;

        var confidence = Math.Min(Math.Abs(directionScore) / 8.0m, 1.0m);
        if (confidence < 0.20m) return; // Not enough signal

        string direction = directionScore > 0 ? "Up" : "Down";
        var effectiveDelta = directionScore / 10.0m; // Normalize for generator

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
            "{Symbol} Score:{Score:F1} Dir:{Dir} Conf:{Conf:F2} Signals:{SigCnt} MktPrice:{Price:F4}",
            candle.Asset.Symbol, directionScore, direction, confidence, signalCount, marketPrice);

        var signalResult = _signalGenerator.Generate(
            candle.Asset, TimeFrame.FiveMinute, oneMinCandles,
            marketPrice, indicators, signalDirection, effectiveDelta);

        if (signalResult.IsSuccess)
        {
            var sig = signalResult.Value!;
            _logger.LogInformation(
                ">>> SIGNAL: {Symbol}/5m {Direction} | FV:{FV:F3} Market:{Market:F3} Edge:{Edge:F3} Score:{Score:F1}",
                sig.Asset.Symbol, sig.Direction, sig.FairValue, sig.MarketPrice, sig.Edge, directionScore);

            _publisher.PublishSignalGenerated(sig.ToDto());

            // Wait 2 seconds for market to open (Golden Rule 2)
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
