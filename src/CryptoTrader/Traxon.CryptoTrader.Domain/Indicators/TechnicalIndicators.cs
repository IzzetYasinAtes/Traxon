using Traxon.CryptoTrader.Domain.Abstractions;
using Traxon.CryptoTrader.Domain.Assets;
using Traxon.CryptoTrader.Domain.Patterns;

namespace Traxon.CryptoTrader.Domain.Indicators;

public sealed class TechnicalIndicators : ValueObject
{
    public Asset Asset { get; }
    public TimeFrame TimeFrame { get; }
    public DateTime CalculatedAt { get; }
    public decimal CurrentPrice { get; }

    public RsiResult Rsi { get; }
    public MacdResult Macd { get; }
    public BollingerBandsResult BollingerBands { get; }
    public AtrResult Atr { get; }
    public VwapResult Vwap { get; }
    public StochasticResult Stochastic { get; }

    /// <summary>SMA(10) hızlı ortalama</summary>
    public decimal FastSma { get; }
    /// <summary>SMA(30) yavaş ortalama</summary>
    public decimal SlowSma { get; }
    public bool IsFastSmaAboveSlow => FastSma > SlowSma;

    /// <summary>Parkinson volatility estimator</summary>
    public decimal ParkinsonVolatility { get; }

    /// <summary>Candlestick pattern analiz sonucu (null ise henüz hesaplanmadı).</summary>
    public PatternAnalysis? PatternAnalysis { get; private set; }

    public TechnicalIndicators(
        Asset asset,
        TimeFrame timeFrame,
        DateTime calculatedAt,
        decimal currentPrice,
        RsiResult rsi,
        MacdResult macd,
        BollingerBandsResult bollingerBands,
        AtrResult atr,
        VwapResult vwap,
        StochasticResult stochastic,
        decimal fastSma,
        decimal slowSma,
        decimal parkinsonVolatility,
        PatternAnalysis? patternAnalysis = null)
    {
        Asset = asset;
        TimeFrame = timeFrame;
        CalculatedAt = calculatedAt;
        CurrentPrice = currentPrice;
        Rsi = rsi;
        Macd = macd;
        BollingerBands = bollingerBands;
        Atr = atr;
        Vwap = vwap;
        Stochastic = stochastic;
        FastSma = fastSma;
        SlowSma = slowSma;
        ParkinsonVolatility = parkinsonVolatility;
        PatternAnalysis = patternAnalysis;
    }

    /// <summary>Multi-confirmation: kaç indicator bullish yön gösteriyor? Pattern bonus dahil.</summary>
    public int BullishCount()
    {
        int count = 0;
        if (Rsi.IsAboveMiddle)               count++;
        if (Macd.IsBullish)                  count++;
        if (Vwap.IsPriceAbove(CurrentPrice)) count++;
        if (IsFastSmaAboveSlow)              count++;
        if (Stochastic.IsKAboveD)            count++;

        // Pattern bonus
        if (PatternAnalysis is { BullishPatternCount: >= 2 }) count++;
        if (PatternAnalysis is { BearishPatternCount: >= 2 }) count--;

        return Math.Max(0, count);
    }

    /// <summary>Multi-confirmation: kaç indicator bearish yön gösteriyor? Pattern bonus dahil.</summary>
    public int BearishCount()
    {
        int count = 0;
        if (!Rsi.IsAboveMiddle)               count++;
        if (!Macd.IsBullish)                  count++;
        if (!Vwap.IsPriceAbove(CurrentPrice)) count++;
        if (!IsFastSmaAboveSlow)              count++;
        if (!Stochastic.IsKAboveD)            count++;

        // Pattern bonus
        if (PatternAnalysis is { BearishPatternCount: >= 2 }) count++;
        if (PatternAnalysis is { BullishPatternCount: >= 2 }) count--;

        return Math.Max(0, count);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Asset;
        yield return TimeFrame;
        yield return CalculatedAt;
    }
}
