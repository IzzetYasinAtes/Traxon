namespace Traxon.CryptoTrader.Domain.Patterns;

/// <summary>Tespit edilen tek bir candlestick pattern.</summary>
/// <param name="PatternType">Pattern türü.</param>
/// <param name="Direction">Bullish, Bearish veya Neutral.</param>
/// <param name="Confidence">Güven skoru (0.0 – 1.0).</param>
/// <param name="StartIndex">Pattern'ın başladığı candle index'i.</param>
/// <param name="EndIndex">Pattern'ın bittiği candle index'i.</param>
public sealed record DetectedPattern(
    CandlestickPatternType PatternType,
    PatternDirection Direction,
    decimal Confidence,
    int StartIndex,
    int EndIndex);
