using Traxon.CryptoTrader.Application.Abstractions;
using Traxon.CryptoTrader.Domain.Assets;
using Traxon.CryptoTrader.Domain.Market;

namespace Traxon.CryptoTrader.Worker.Workers;

public sealed class MarketDataWorker : BackgroundService
{
    private readonly IMarketDataProvider _marketDataProvider;
    private readonly ICandleBuffer _candleBuffer;
    private readonly IIndicatorCalculator _indicatorCalculator;
    private readonly ILogger<MarketDataWorker> _logger;

    public MarketDataWorker(
        IMarketDataProvider marketDataProvider,
        ICandleBuffer candleBuffer,
        IIndicatorCalculator indicatorCalculator,
        ILogger<MarketDataWorker> logger)
    {
        _marketDataProvider = marketDataProvider;
        _candleBuffer = candleBuffer;
        _indicatorCalculator = indicatorCalculator;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MarketDataWorker starting — loading historical candles...");

        foreach (var asset in Asset.All)
        foreach (var tf in TimeFrame.All)
        {
            var candles = await _marketDataProvider.GetHistoricalCandlesAsync(
                asset, tf, limit: 200, stoppingToken);

            foreach (var candle in candles)
                _candleBuffer.Add(candle);

            _logger.LogInformation("Loaded {Count} candles for {Symbol}/{Interval}",
                candles.Count, asset.Symbol, tf.Value);
        }

        _logger.LogInformation("Buffer warm-up complete. Starting WebSocket stream...");

        await _marketDataProvider.StartStreamAsync(
            assets: Asset.All,
            timeFrames: TimeFrame.All,
            onCandleClosed: OnCandleClosedAsync,
            cancellationToken: stoppingToken);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task OnCandleClosedAsync(Candle candle)
    {
        _candleBuffer.Add(candle);

        if (!_candleBuffer.IsWarmedUp(candle.Asset, candle.TimeFrame, minimumCandles: 30))
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
            return;
        }

        var indicators = indicatorResult.Value!;
        _logger.LogInformation(
            "{Symbol}/{Interval} — RSI:{Rsi:F1} MACD:{Macd:F6} BB({Lower:F2}/{Upper:F2}) ATR:{Atr:F6} Bulls:{Bulls}/5",
            candle.Asset.Symbol, candle.TimeFrame.Value,
            indicators.Rsi.Value,
            indicators.Macd.Histogram,
            indicators.BollingerBands.Lower, indicators.BollingerBands.Upper,
            indicators.Atr.Value,
            indicators.BullishCount());

        await Task.CompletedTask;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await _marketDataProvider.StopStreamAsync(cancellationToken);
        await base.StopAsync(cancellationToken);
    }
}
