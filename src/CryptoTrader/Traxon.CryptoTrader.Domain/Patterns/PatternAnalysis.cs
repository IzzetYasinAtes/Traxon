namespace Traxon.CryptoTrader.Domain.Patterns;

/// <summary>Candlestick pattern analiz sonucu.</summary>
/// <param name="CandlestickPatterns">Tespit edilen pattern'lar.</param>
/// <param name="PatternBias">Toplam yön eğilimi (−1.0 bearish … +1.0 bullish).</param>
/// <param name="BullishPatternCount">Toplam bullish pattern sayısı.</param>
/// <param name="BearishPatternCount">Toplam bearish pattern sayısı.</param>
public sealed record PatternAnalysis(
    IReadOnlyList<DetectedPattern> CandlestickPatterns,
    decimal PatternBias,
    int BullishPatternCount,
    int BearishPatternCount);
