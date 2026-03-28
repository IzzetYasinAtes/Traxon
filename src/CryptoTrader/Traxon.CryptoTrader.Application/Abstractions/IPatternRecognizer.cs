using Traxon.CryptoTrader.Domain.Market;
using Traxon.CryptoTrader.Domain.Patterns;

namespace Traxon.CryptoTrader.Application.Abstractions;

/// <summary>Candlestick pattern tanıma servisi.</summary>
public interface IPatternRecognizer
{
    /// <summary>Candle listesinden tüm candlestick pattern'ları tespit eder.</summary>
    IReadOnlyList<DetectedPattern> DetectCandlestickPatterns(IReadOnlyList<Candle> candles);

    /// <summary>Pattern tespiti yapar ve bias/sayı bilgisiyle analiz döndürür.</summary>
    PatternAnalysis Analyze(IReadOnlyList<Candle> candles);
}
