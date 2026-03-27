using Traxon.CryptoTrader.Application.Abstractions;
using Traxon.CryptoTrader.Domain.Assets;
using Traxon.CryptoTrader.Domain.Market;

namespace Traxon.CryptoTrader.Worker.Workers;

public sealed class MarketDataWorker : BackgroundService
{
    private readonly IMarketDataProvider _marketDataProvider;
    private readonly ICandleBuffer _candleBuffer;
    private readonly IIndicatorCalculator _indicatorCalculator;
    private readonly ISignalGenerator _signalGenerator;
    private readonly ILogger<MarketDataWorker> _logger;

    public MarketDataWorker(
        IMarketDataProvider marketDataProvider,
        ICandleBuffer candleBuffer,
        IIndicatorCalculator indicatorCalculator,
        ISignalGenerator signalGenerator,
        ILogger<MarketDataWorker> logger)
    {
        _marketDataProvider  = marketDataProvider;
        _candleBuffer        = candleBuffer;
        _indicatorCalculator = indicatorCalculator;
        _signalGenerator     = signalGenerator;
        _logger              = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MarketDataWorker starting — loading historical candles...");

        foreach (var asset in Asset.All)
        foreach (var tf in TimeFrame.All)
        {
            var candlesResult = await _marketDataProvider.GetHistoricalCandlesAsync(
                asset, tf, limit: 200, stoppingToken);

            if (candlesResult.IsFailure)
            {
                _logger.LogWarning("Failed to load historical candles for {Symbol}/{Interval}: {Error}",
                    asset.Symbol, tf.Value, candlesResult.Error!.Message);
                continue;
            }

            foreach (var candle in candlesResult.Value!)
                _candleBuffer.Add(candle);

            _logger.LogInformation("Loaded {Count} candles for {Symbol}/{Interval}",
                candlesResult.Value!.Count, asset.Symbol, tf.Value);
        }

        _logger.LogInformation("Buffer warm-up complete. Starting WebSocket stream...");

        await _marketDataProvider.StartStreamAsync(
            assets: Asset.All,
            timeFrames: TimeFrame.All,
            onCandleClosed: OnCandleClosedAsync,
            cancellationToken: stoppingToken);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private Task OnCandleClosedAsync(Candle candle)
    {
        _candleBuffer.Add(candle);

        if (!_candleBuffer.IsWarmedUp(candle.Asset, candle.TimeFrame, minimumCandles: 30))
        {
            _logger.LogDebug("Buffer not warmed up yet for {Symbol}/{Interval}",
                candle.Asset.Symbol, candle.TimeFrame.Value);
            return Task.CompletedTask;
        }

        var candlesResult = _candleBuffer.GetAll(candle.Asset, candle.TimeFrame);
        if (candlesResult.IsFailure) return Task.CompletedTask;

        var indicatorResult = _indicatorCalculator.Calculate(
            candle.Asset, candle.TimeFrame, candlesResult.Value!);

        if (indicatorResult.IsFailure)
        {
            _logger.LogWarning("Indicator calc failed for {Symbol}/{Interval}: {Error}",
                candle.Asset.Symbol, candle.TimeFrame.Value, indicatorResult.Error!.Message);
            return Task.CompletedTask;
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

        // Signal uret (simulated market price — Faz 3'te Polymarket API'dan gelecek)
        const decimal simulatedMarketPrice = 0.50m;
        var signalResult = _signalGenerator.Generate(
            candle.Asset, candle.TimeFrame, candlesResult.Value!, simulatedMarketPrice);

        if (signalResult.IsSuccess)
        {
            var sig = signalResult.Value!;
            _logger.LogInformation(
                ">>> SIGNAL: {Symbol}/{Interval} {Direction} | FV:{FV:F3} Edge:{Edge:F3} " +
                "Kelly:{Kelly:F4} Regime:{Regime} | Mu:{Mu:E3} Sigma:{Sigma:E3}",
                sig.Asset.Symbol, sig.TimeFrame.Value, sig.Direction,
                sig.FairValue, sig.Edge, sig.KellyFraction, sig.Regime,
                sig.MuEstimate, sig.SigmaEstimate);
        }
        else
        {
            _logger.LogDebug("No signal: {Symbol}/{Interval} — {Reason}",
                candle.Asset.Symbol, candle.TimeFrame.Value, signalResult.Error!.Code);
        }

        return Task.CompletedTask;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await _marketDataProvider.StopStreamAsync(cancellationToken);
        await base.StopAsync(cancellationToken);
    }
}
