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

    /// <summary>RSI(7) kisa vadeli momentum (null ise henuz hesaplanmadi).</summary>
    public RsiResult? RsiShort { get; }

    /// <summary>EMA(9) sonucu (null ise henuz hesaplanmadi).</summary>
    public EmaResult? Ema9 { get; }

    /// <summary>Hacim analizi (null ise henuz hesaplanmadi).</summary>
    public VolumeResult? Volume { get; }

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
        PatternAnalysis? patternAnalysis = null,
        RsiResult? rsiShort = null,
        EmaResult? ema9 = null,
        VolumeResult? volume = null)
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
        RsiShort = rsiShort;
        Ema9 = ema9;
        Volume = volume;
    }

    /// <summary>
    /// Agirlikli sinyal skoru hesaplar (0.0 - 1.0 arasi).
    /// RSI7(%25) + MACD(%20) + BB(%20) + EMA9(%15) + Volume(%10) + RSI14(%10)
    /// </summary>
    public decimal CalculateSignalScore()
    {
        decimal score = 0m;

        // RSI7 (%25) — 0-100 arasini 0-1'e normalize et
        if (RsiShort is not null)
            score += 0.25m * (RsiShort.Value / 100m);
        else
            score += 0.25m * (Rsi.Value / 100m); // fallback RSI14

        // MACD (%20) — histogram pozitif ise bullish
        score += 0.20m * (Macd.IsBullish ? 1m : 0m);

        // Bollinger Bands (%20) — fiyat lower band'e yakinsa oversold (bullish sinyal)
        var bbRange = BollingerBands.Upper - BollingerBands.Lower;
        if (bbRange > 0)
        {
            var bbPosition = (CurrentPrice - BollingerBands.Lower) / bbRange;
            score += 0.20m * Math.Clamp(bbPosition, 0m, 1m);
        }
        else
        {
            score += 0.10m; // neutral
        }

        // EMA9 (%15) — fiyat EMA ustunde ve rising ise bullish
        if (Ema9 is not null)
        {
            var emaScore = 0m;
            if (CurrentPrice > Ema9.Value) emaScore += 0.5m;
            if (Ema9.IsRising) emaScore += 0.5m;
            score += 0.15m * emaScore;
        }
        else
        {
            score += 0.15m * (IsFastSmaAboveSlow ? 1m : 0m); // fallback SMA
        }

        // Volume (%10) — ortalamanin ustunde ise dogrulama
        if (Volume is not null)
        {
            score += 0.10m * Math.Clamp(Volume.Ratio / 2m, 0m, 1m); // ratio=2 -> max skor
        }
        else
        {
            score += 0.05m; // neutral
        }

        // RSI14 (%10) — uzun vadeli trend
        score += 0.10m * (Rsi.Value / 100m);

        return Math.Clamp(score, 0m, 1m);
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
