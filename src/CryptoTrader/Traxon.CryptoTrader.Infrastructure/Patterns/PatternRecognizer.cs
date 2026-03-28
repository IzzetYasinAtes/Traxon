using Traxon.CryptoTrader.Application.Abstractions;
using Traxon.CryptoTrader.Domain.Market;
using Traxon.CryptoTrader.Domain.Patterns;

namespace Traxon.CryptoTrader.Infrastructure.Patterns;

/// <summary>Candlestick pattern tanıma servisi implementasyonu.</summary>
public sealed class PatternRecognizer : IPatternRecognizer
{
    private const decimal BiasPerPattern = 0.10m;

    /// <inheritdoc />
    public IReadOnlyList<DetectedPattern> DetectCandlestickPatterns(IReadOnlyList<Candle> candles) =>
        CandlestickPatternDetector.DetectAll(candles);

    /// <inheritdoc />
    public PatternAnalysis Analyze(IReadOnlyList<Candle> candles)
    {
        var patterns = CandlestickPatternDetector.DetectAll(candles);

        int bullish = 0;
        int bearish = 0;

        foreach (var p in patterns)
        {
            switch (p.Direction)
            {
                case PatternDirection.Bullish: bullish++; break;
                case PatternDirection.Bearish: bearish++; break;
            }
        }

        var rawBias = (bullish - bearish) * BiasPerPattern;
        var bias = Math.Clamp(rawBias, -1m, 1m);

        return new PatternAnalysis(patterns, bias, bullish, bearish);
    }
}
