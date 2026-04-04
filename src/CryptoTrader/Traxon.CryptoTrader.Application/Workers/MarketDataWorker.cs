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

    /// <summary>TimeFrame bazli backfill limit: 1m→120, 5m→500, 15m→500, 1h→48.</summary>
    private static int GetBackfillLimit(TimeFrame tf) => tf.Value switch
    {
        "1m"  => 120,
        "5m"  => 500,
        "15m" => 500,
        "1h"  => 48,
        _     => 200
    };

    /// <summary>TimeFrame bazli warmup esigi: 1m→30, 5m→100, 15m→100, 1h→24.</summary>
    private static int GetWarmupThreshold(TimeFrame tf) => tf.Value switch
    {
        "1m"  => 30,
        "5m"  => 100,
        "15m" => 100,
        "1h"  => 24,
        _     => 50
    };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MarketDataWorker starting — loading historical candles...");

        foreach (var asset in Asset.Tradeable)
        foreach (var tf in TimeFrame.All)
        {
            var candlesResult = await _marketDataProvider.GetHistoricalCandlesAsync(
                asset, tf, limit: GetBackfillLimit(tf), stoppingToken);

            if (candlesResult.IsFailure)
            {
                _logger.LogWarning("Failed to load historical candles for {Symbol}/{Interval}: {Error}",
                    asset.Symbol, tf.Value, candlesResult.Error!.Message);
                continue;
            }

            foreach (var candle in candlesResult.Value!)
            {
                _candleBuffer.Add(candle);
                _ = _candleWriter.WriteAsync(candle, stoppingToken);
            }

            _logger.LogInformation("Loaded {Count} candles for {Symbol}/{Interval}",
                candlesResult.Value!.Count, asset.Symbol, tf.Value);
        }

        _logger.LogInformation("Buffer warm-up complete. Starting WebSocket stream...");

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

    private async Task OnCandleClosedAsync(Candle candle)
    {
        _candleBuffer.Add(candle);

        // Ticker guncelle — onceki close ile karsilastir
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

        // Candle guncelle
        _publisher.PublishCandleUpdate(candle.ToCandleDto());

        // SQL'e async yaz (fire-and-forget — hata sinyal uretimini ENGELLEMEMELI)
        _ = Task.Run(async () =>
        {
            try { await _candleWriter.WriteAsync(candle); }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Candle write failed for {Symbol}, signal generation continues",
                    candle.Asset.Symbol);
            }
        });

        // Polymarket sadece 5 dakikalık market sunuyor — sadece 5m mumlar sinyal üretir
        if (candle.TimeFrame != TimeFrame.FiveMinute)
            return;

        if (!_candleBuffer.IsWarmedUp(candle.Asset, candle.TimeFrame, minimumCandles: GetWarmupThreshold(candle.TimeFrame)))
        {
            _logger.LogDebug("Buffer not warmed up yet for {Symbol}/{Interval}",
                candle.Asset.Symbol, candle.TimeFrame.Value);
            return;
        }

        var candlesResult = _candleBuffer.GetAll(candle.Asset, candle.TimeFrame);
        if (candlesResult.IsFailure) return;

        var indicatorResult = _indicatorCalculator.Calculate(
            candle.Asset, candle.TimeFrame, candlesResult.Value!);

        if (indicatorResult.IsFailure)
        {
            _logger.LogWarning("Indicator calc failed for {Symbol}/{Interval}: {Error}",
                candle.Asset.Symbol, candle.TimeFrame.Value, indicatorResult.Error!.Message);
        }
        else
        {
            var indicators = indicatorResult.Value!;
            _logger.LogInformation(
                "{Symbol}/{Interval} — RSI:{Rsi:F1} MACD:{Macd:F6} BB({Lower:F2}/{Upper:F2}) ATR:{Atr:F6} Bulls:{Bulls}/5",
                candle.Asset.Symbol, candle.TimeFrame.Value,
                indicators.Rsi.Value,
                indicators.Macd.Histogram,
                indicators.BollingerBands.Lower, indicators.BollingerBands.Upper,
                indicators.Atr.Value,
                indicators.BullishCount());

            // Fetch real Polymarket midpoint — sinyal üretimi için
            do
            {
                var direction = indicators.BullishCount() >= 3 ? "Up" : "Down";
                var baseAsset = candle.Asset.Symbol.Replace("USDT", "");

                var discoverResult = await _discovery.DiscoverMarketsAsync();
                if (discoverResult.IsFailure)
                {
                    _logger.LogWarning("Polymarket market discovery failed for {Symbol}, skipping signal", candle.Asset.Symbol);
                    break;
                }

                var market = discoverResult.Value!
                    .FirstOrDefault(m => m.UnderlyingAsset.Equals(baseAsset, StringComparison.OrdinalIgnoreCase)
                                      && m.Direction == direction);
                if (market is null)
                {
                    _logger.LogDebug("No Polymarket market for {Symbol} {Direction}, skipping signal", candle.Asset.Symbol, direction);
                    break;
                }

                var midResult = await _polyClient.GetMidpointAsync(market.RelevantTokenId);
                if (midResult.IsFailure)
                {
                    _logger.LogWarning("Polymarket midpoint failed for {Symbol}, skipping signal", candle.Asset.Symbol);
                    break;
                }

                var marketPrice = midResult.Value;

                // FairValue = P(Up). Down token midpoint = P(Down). Convert to same basis.
                if (direction == "Down")
                    marketPrice = 1m - marketPrice;

                var signalResult = _signalGenerator.Generate(
                    candle.Asset, candle.TimeFrame, candlesResult.Value!,
                    marketPrice, indicators);

                if (signalResult.IsSuccess)
                {
                    var sig = signalResult.Value!;
                    _logger.LogInformation(
                        ">>> SIGNAL: {Symbol}/{Interval} {Direction} | FV:{FV:F3} Market:{Market:F3} Edge:{Edge:F3} " +
                        "Kelly:{Kelly:F4} Regime:{Regime} Bulls:{Bulls}/5",
                        sig.Asset.Symbol, sig.TimeFrame.Value, sig.Direction,
                        sig.FairValue, sig.MarketPrice, sig.Edge, sig.KellyFraction, sig.Regime,
                        sig.Indicators.BullishCount());

                    _publisher.PublishSignalGenerated(sig.ToDto());

                    // Dispatch to all engines in PARALLEL to avoid timing gaps
                    // (e.g. LivePoly calling DiscoverMarketsAsync 1-2s after PaperPoly,
                    //  by which time a 5-min market may have closed)
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
                else
                {
                    _logger.LogDebug("No signal: {Symbol}/{Interval} — {Reason}",
                        candle.Asset.Symbol, candle.TimeFrame.Value, signalResult.Error!.Code);
                }
            } while (false);
        }

        foreach (var engine in _tradingEngines)
        {
            await engine.CheckPositionsAsync(candle);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await _marketDataProvider.StopStreamAsync(cancellationToken);
        await base.StopAsync(cancellationToken);
    }
}
