using Traxon.CryptoTrader.Domain.Market;
using Traxon.CryptoTrader.Domain.Patterns;

namespace Traxon.CryptoTrader.Infrastructure.Patterns;

/// <summary>30 candlestick pattern için rule-based tespit motoru.</summary>
public static class CandlestickPatternDetector
{
    private const decimal DojiThreshold = 0.05m;
    private const decimal MarubozuThreshold = 0.02m;
    private const decimal SpinningTopMaxBody = 0.30m;
    private const decimal TweezerTolerance = 0.001m;

    // ─────────────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Son candle'lar üzerinde tüm pattern'ları tarar.</summary>
    public static IReadOnlyList<DetectedPattern> DetectAll(IReadOnlyList<Candle> candles)
    {
        if (candles.Count < 1) return [];

        var patterns = new List<DetectedPattern>();
        int last = candles.Count - 1;

        // Single candle patterns (son mum)
        DetectSingleCandle(candles, last, patterns);

        // Two candle patterns (son 2 mum)
        if (candles.Count >= 2)
            DetectTwoCandle(candles, last, patterns);

        // Three+ candle patterns (son 3+ mum)
        if (candles.Count >= 3)
            DetectThreeCandle(candles, last, patterns);

        return patterns;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static decimal Body(Candle c) => Math.Abs(c.Close - c.Open);
    private static decimal Range(Candle c) => c.High - c.Low;
    private static decimal UpperShadow(Candle c) => c.High - Math.Max(c.Open, c.Close);
    private static decimal LowerShadow(Candle c) => Math.Min(c.Open, c.Close) - c.Low;
    private static decimal MidPoint(Candle c) => (c.Open + c.Close) / 2m;

    private static bool IsDoji(Candle c)
    {
        var range = Range(c);
        return range > 0 && Body(c) < DojiThreshold * range;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Single Candle Patterns
    // ─────────────────────────────────────────────────────────────────────────

    private static void DetectSingleCandle(
        IReadOnlyList<Candle> candles, int idx, List<DetectedPattern> results)
    {
        var c = candles[idx];
        var range = Range(c);
        if (range == 0) return;

        var body = Body(c);
        var upper = UpperShadow(c);
        var lower = LowerShadow(c);

        // Doji variants
        if (IsDoji(c))
        {
            if (lower > 2m * body && upper < 0.1m * range)
                results.Add(new DetectedPattern(CandlestickPatternType.DragonflyDoji, PatternDirection.Bullish, 0.7m, idx, idx));
            else if (upper > 2m * body && lower < 0.1m * range)
                results.Add(new DetectedPattern(CandlestickPatternType.GravestoneDoji, PatternDirection.Bearish, 0.7m, idx, idx));
            else
                results.Add(new DetectedPattern(CandlestickPatternType.Doji, PatternDirection.Neutral, 0.6m, idx, idx));

            return; // Doji detected — skip other single patterns
        }

        // Hammer: long lower shadow, small upper shadow, preceding downtrend
        if (DetectHammer(candles, idx, body, range, upper, lower) is { } hammer)
            results.Add(hammer);

        // Inverted Hammer: long upper shadow, small lower shadow, preceding downtrend
        if (DetectInvertedHammer(candles, idx, body, range, upper, lower) is { } invHammer)
            results.Add(invHammer);

        // Shooting Star: long upper shadow, small lower shadow, preceding uptrend
        if (DetectShootingStar(candles, idx, body, range, upper, lower) is { } shootingStar)
            results.Add(shootingStar);

        // Hanging Man: long lower shadow, small upper shadow, preceding uptrend
        if (DetectHangingMan(candles, idx, body, range, upper, lower) is { } hangingMan)
            results.Add(hangingMan);

        // Marubozu: near-zero shadows
        if (DetectMarubozu(c, idx, body, range, upper, lower) is { } marubozu)
            results.Add(marubozu);

        // Spinning Top: small body, roughly equal shadows
        if (DetectSpinningTop(c, idx, body, range, upper, lower) is { } spinningTop)
            results.Add(spinningTop);
    }

    private static DetectedPattern? DetectHammer(
        IReadOnlyList<Candle> candles, int idx,
        decimal body, decimal range, decimal upper, decimal lower)
    {
        if (lower < 2m * body || upper > 0.3m * range) return null;
        if (!HasPrecedingDowntrend(candles, idx, 3)) return null;
        return new DetectedPattern(CandlestickPatternType.Hammer, PatternDirection.Bullish, 0.75m, idx, idx);
    }

    private static DetectedPattern? DetectInvertedHammer(
        IReadOnlyList<Candle> candles, int idx,
        decimal body, decimal range, decimal upper, decimal lower)
    {
        if (upper < 2m * body || lower > 0.3m * range) return null;
        if (!HasPrecedingDowntrend(candles, idx, 3)) return null;
        return new DetectedPattern(CandlestickPatternType.InvertedHammer, PatternDirection.Bullish, 0.65m, idx, idx);
    }

    private static DetectedPattern? DetectShootingStar(
        IReadOnlyList<Candle> candles, int idx,
        decimal body, decimal range, decimal upper, decimal lower)
    {
        if (upper < 2m * body || lower > 0.3m * range) return null;
        if (!HasPrecedingUptrend(candles, idx, 3)) return null;
        return new DetectedPattern(CandlestickPatternType.ShootingStar, PatternDirection.Bearish, 0.75m, idx, idx);
    }

    private static DetectedPattern? DetectHangingMan(
        IReadOnlyList<Candle> candles, int idx,
        decimal body, decimal range, decimal upper, decimal lower)
    {
        if (lower < 2m * body || upper > 0.3m * range) return null;
        if (!HasPrecedingUptrend(candles, idx, 3)) return null;
        return new DetectedPattern(CandlestickPatternType.HangingMan, PatternDirection.Bearish, 0.70m, idx, idx);
    }

    private static DetectedPattern? DetectMarubozu(
        Candle c, int idx,
        decimal body, decimal range, decimal upper, decimal lower)
    {
        if (upper > MarubozuThreshold * range || lower > MarubozuThreshold * range) return null;
        if (body < 0.8m * range) return null;

        return c.IsBullish
            ? new DetectedPattern(CandlestickPatternType.BullishMarubozu, PatternDirection.Bullish, 0.80m, idx, idx)
            : new DetectedPattern(CandlestickPatternType.BearishMarubozu, PatternDirection.Bearish, 0.80m, idx, idx);
    }

    private static DetectedPattern? DetectSpinningTop(
        Candle c, int idx,
        decimal body, decimal range, decimal upper, decimal lower)
    {
        if (body > SpinningTopMaxBody * range) return null;
        if (upper == 0 || lower == 0) return null;

        var shadowRatio = upper > lower ? lower / upper : upper / lower;
        if (shadowRatio < 0.4m) return null;

        return new DetectedPattern(CandlestickPatternType.SpinningTop, PatternDirection.Neutral, 0.50m, idx, idx);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Two Candle Patterns
    // ─────────────────────────────────────────────────────────────────────────

    private static void DetectTwoCandle(
        IReadOnlyList<Candle> candles, int idx, List<DetectedPattern> results)
    {
        var prev = candles[idx - 1];
        var curr = candles[idx];

        if (DetectBullishEngulfing(prev, curr, idx) is { } be) results.Add(be);
        if (DetectBearishEngulfing(prev, curr, idx) is { } bea) results.Add(bea);
        if (DetectBullishHarami(prev, curr, idx) is { } bh) results.Add(bh);
        if (DetectBearishHarami(prev, curr, idx) is { } bearH) results.Add(bearH);
        if (DetectPiercingLine(prev, curr, idx) is { } pl) results.Add(pl);
        if (DetectDarkCloudCover(prev, curr, idx) is { } dcc) results.Add(dcc);
        if (DetectTweezerTop(prev, curr, idx) is { } tt) results.Add(tt);
        if (DetectTweezerBottom(prev, curr, idx) is { } tb) results.Add(tb);
        if (DetectBullishKicker(prev, curr, idx) is { } bk) results.Add(bk);
        if (DetectBearishKicker(prev, curr, idx) is { } bearK) results.Add(bearK);
    }

    private static DetectedPattern? DetectBullishEngulfing(Candle prev, Candle curr, int idx)
    {
        if (!prev.IsBearish || !curr.IsBullish) return null;
        if (curr.Open >= prev.Close || curr.Close <= prev.Open) return null;
        return new DetectedPattern(CandlestickPatternType.BullishEngulfing, PatternDirection.Bullish, 0.80m, idx - 1, idx);
    }

    private static DetectedPattern? DetectBearishEngulfing(Candle prev, Candle curr, int idx)
    {
        if (!prev.IsBullish || !curr.IsBearish) return null;
        if (curr.Open <= prev.Close || curr.Close >= prev.Open) return null;
        return new DetectedPattern(CandlestickPatternType.BearishEngulfing, PatternDirection.Bearish, 0.80m, idx - 1, idx);
    }

    private static DetectedPattern? DetectBullishHarami(Candle prev, Candle curr, int idx)
    {
        if (!prev.IsBearish || !curr.IsBullish) return null;
        if (curr.Close > prev.Open || curr.Open < prev.Close) return null;
        if (Body(curr) >= Body(prev)) return null;
        return new DetectedPattern(CandlestickPatternType.BullishHarami, PatternDirection.Bullish, 0.65m, idx - 1, idx);
    }

    private static DetectedPattern? DetectBearishHarami(Candle prev, Candle curr, int idx)
    {
        if (!prev.IsBullish || !curr.IsBearish) return null;
        if (curr.Open > prev.Close || curr.Close < prev.Open) return null;
        if (Body(curr) >= Body(prev)) return null;
        return new DetectedPattern(CandlestickPatternType.BearishHarami, PatternDirection.Bearish, 0.65m, idx - 1, idx);
    }

    private static DetectedPattern? DetectPiercingLine(Candle prev, Candle curr, int idx)
    {
        if (!prev.IsBearish || !curr.IsBullish) return null;
        if (curr.Open >= prev.Close) return null;
        if (curr.Close <= MidPoint(prev)) return null;
        if (curr.Close >= prev.Open) return null;
        return new DetectedPattern(CandlestickPatternType.PiercingLine, PatternDirection.Bullish, 0.70m, idx - 1, idx);
    }

    private static DetectedPattern? DetectDarkCloudCover(Candle prev, Candle curr, int idx)
    {
        if (!prev.IsBullish || !curr.IsBearish) return null;
        if (curr.Open <= prev.Close) return null;
        if (curr.Close >= MidPoint(prev)) return null;
        if (curr.Close <= prev.Open) return null;
        return new DetectedPattern(CandlestickPatternType.DarkCloudCover, PatternDirection.Bearish, 0.70m, idx - 1, idx);
    }

    private static DetectedPattern? DetectTweezerTop(Candle prev, Candle curr, int idx)
    {
        if (prev.High == 0) return null;
        var diff = Math.Abs(prev.High - curr.High) / prev.High;
        if (diff > TweezerTolerance) return null;
        if (!prev.IsBullish || !curr.IsBearish) return null;
        return new DetectedPattern(CandlestickPatternType.TweezerTop, PatternDirection.Bearish, 0.65m, idx - 1, idx);
    }

    private static DetectedPattern? DetectTweezerBottom(Candle prev, Candle curr, int idx)
    {
        if (prev.Low == 0) return null;
        var diff = Math.Abs(prev.Low - curr.Low) / prev.Low;
        if (diff > TweezerTolerance) return null;
        if (!prev.IsBearish || !curr.IsBullish) return null;
        return new DetectedPattern(CandlestickPatternType.TweezerBottom, PatternDirection.Bullish, 0.65m, idx - 1, idx);
    }

    private static DetectedPattern? DetectBullishKicker(Candle prev, Candle curr, int idx)
    {
        if (!prev.IsBearish || !curr.IsBullish) return null;
        if (curr.Open <= prev.Open) return null; // gap up
        return new DetectedPattern(CandlestickPatternType.BullishKicker, PatternDirection.Bullish, 0.85m, idx - 1, idx);
    }

    private static DetectedPattern? DetectBearishKicker(Candle prev, Candle curr, int idx)
    {
        if (!prev.IsBullish || !curr.IsBearish) return null;
        if (curr.Open >= prev.Open) return null; // gap down
        return new DetectedPattern(CandlestickPatternType.BearishKicker, PatternDirection.Bearish, 0.85m, idx - 1, idx);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Three+ Candle Patterns
    // ─────────────────────────────────────────────────────────────────────────

    private static void DetectThreeCandle(
        IReadOnlyList<Candle> candles, int idx, List<DetectedPattern> results)
    {
        var first = candles[idx - 2];
        var second = candles[idx - 1];
        var third = candles[idx];

        if (DetectMorningStar(first, second, third, idx) is { } ms) results.Add(ms);
        if (DetectEveningStar(first, second, third, idx) is { } es) results.Add(es);
        if (DetectMorningDojiStar(first, second, third, idx) is { } mds) results.Add(mds);
        if (DetectEveningDojiStar(first, second, third, idx) is { } eds) results.Add(eds);
        if (DetectThreeWhiteSoldiers(first, second, third, idx) is { } tws) results.Add(tws);
        if (DetectThreeBlackCrows(first, second, third, idx) is { } tbc) results.Add(tbc);
        if (DetectThreeInsideUp(first, second, third, idx) is { } tiu) results.Add(tiu);
        if (DetectThreeInsideDown(first, second, third, idx) is { } tid) results.Add(tid);
        if (DetectThreeOutsideUp(first, second, third, idx) is { } tou) results.Add(tou);
        if (DetectThreeOutsideDown(first, second, third, idx) is { } tod) results.Add(tod);
    }

    private static DetectedPattern? DetectMorningStar(Candle first, Candle second, Candle third, int idx)
    {
        if (!first.IsBearish) return null;
        if (Body(second) >= 0.3m * Body(first)) return null; // small body
        if (!third.IsBullish) return null;
        if (third.Close <= MidPoint(first)) return null;
        return new DetectedPattern(CandlestickPatternType.MorningStar, PatternDirection.Bullish, 0.80m, idx - 2, idx);
    }

    private static DetectedPattern? DetectEveningStar(Candle first, Candle second, Candle third, int idx)
    {
        if (!first.IsBullish) return null;
        if (Body(second) >= 0.3m * Body(first)) return null;
        if (!third.IsBearish) return null;
        if (third.Close >= MidPoint(first)) return null;
        return new DetectedPattern(CandlestickPatternType.EveningStar, PatternDirection.Bearish, 0.80m, idx - 2, idx);
    }

    private static DetectedPattern? DetectMorningDojiStar(Candle first, Candle second, Candle third, int idx)
    {
        if (!first.IsBearish) return null;
        if (!IsDoji(second)) return null;
        if (!third.IsBullish) return null;
        if (third.Close <= MidPoint(first)) return null;
        return new DetectedPattern(CandlestickPatternType.MorningDojiStar, PatternDirection.Bullish, 0.85m, idx - 2, idx);
    }

    private static DetectedPattern? DetectEveningDojiStar(Candle first, Candle second, Candle third, int idx)
    {
        if (!first.IsBullish) return null;
        if (!IsDoji(second)) return null;
        if (!third.IsBearish) return null;
        if (third.Close >= MidPoint(first)) return null;
        return new DetectedPattern(CandlestickPatternType.EveningDojiStar, PatternDirection.Bearish, 0.85m, idx - 2, idx);
    }

    private static DetectedPattern? DetectThreeWhiteSoldiers(Candle first, Candle second, Candle third, int idx)
    {
        if (!first.IsBullish || !second.IsBullish || !third.IsBullish) return null;
        if (second.Close <= first.Close || third.Close <= second.Close) return null;
        if (second.Open < first.Open || third.Open < second.Open) return null;

        // Each candle should have small upper shadows
        if (UpperShadow(first) > 0.3m * Body(first)) return null;
        if (UpperShadow(second) > 0.3m * Body(second)) return null;
        if (UpperShadow(third) > 0.3m * Body(third)) return null;

        return new DetectedPattern(CandlestickPatternType.ThreeWhiteSoldiers, PatternDirection.Bullish, 0.85m, idx - 2, idx);
    }

    private static DetectedPattern? DetectThreeBlackCrows(Candle first, Candle second, Candle third, int idx)
    {
        if (!first.IsBearish || !second.IsBearish || !third.IsBearish) return null;
        if (second.Close >= first.Close || third.Close >= second.Close) return null;
        if (second.Open > first.Open || third.Open > second.Open) return null;

        if (LowerShadow(first) > 0.3m * Body(first)) return null;
        if (LowerShadow(second) > 0.3m * Body(second)) return null;
        if (LowerShadow(third) > 0.3m * Body(third)) return null;

        return new DetectedPattern(CandlestickPatternType.ThreeBlackCrows, PatternDirection.Bearish, 0.85m, idx - 2, idx);
    }

    private static DetectedPattern? DetectThreeInsideUp(Candle first, Candle second, Candle third, int idx)
    {
        // First: bearish, Second: bullish harami inside first, Third: bullish close above first open
        if (!first.IsBearish || !second.IsBullish || !third.IsBullish) return null;
        if (second.Close > first.Open || second.Open < first.Close) return null;
        if (Body(second) >= Body(first)) return null;
        if (third.Close <= first.Open) return null;
        return new DetectedPattern(CandlestickPatternType.ThreeInsideUp, PatternDirection.Bullish, 0.75m, idx - 2, idx);
    }

    private static DetectedPattern? DetectThreeInsideDown(Candle first, Candle second, Candle third, int idx)
    {
        if (!first.IsBullish || !second.IsBearish || !third.IsBearish) return null;
        if (second.Open > first.Close || second.Close < first.Open) return null;
        if (Body(second) >= Body(first)) return null;
        if (third.Close >= first.Open) return null;
        return new DetectedPattern(CandlestickPatternType.ThreeInsideDown, PatternDirection.Bearish, 0.75m, idx - 2, idx);
    }

    private static DetectedPattern? DetectThreeOutsideUp(Candle first, Candle second, Candle third, int idx)
    {
        // First: bearish, Second: bullish engulfing first, Third: bullish higher close
        if (!first.IsBearish || !second.IsBullish || !third.IsBullish) return null;
        if (second.Open >= first.Close || second.Close <= first.Open) return null;
        if (third.Close <= second.Close) return null;
        return new DetectedPattern(CandlestickPatternType.ThreeOutsideUp, PatternDirection.Bullish, 0.80m, idx - 2, idx);
    }

    private static DetectedPattern? DetectThreeOutsideDown(Candle first, Candle second, Candle third, int idx)
    {
        if (!first.IsBullish || !second.IsBearish || !third.IsBearish) return null;
        if (second.Open <= first.Close || second.Close >= first.Open) return null;
        if (third.Close >= second.Close) return null;
        return new DetectedPattern(CandlestickPatternType.ThreeOutsideDown, PatternDirection.Bearish, 0.80m, idx - 2, idx);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Trend helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static bool HasPrecedingDowntrend(IReadOnlyList<Candle> candles, int idx, int lookback)
    {
        if (idx < lookback) return false;
        int bearishCount = 0;
        for (int i = idx - lookback; i < idx; i++)
        {
            if (candles[i].IsBearish) bearishCount++;
        }
        return bearishCount >= 2;
    }

    private static bool HasPrecedingUptrend(IReadOnlyList<Candle> candles, int idx, int lookback)
    {
        if (idx < lookback) return false;
        int bullishCount = 0;
        for (int i = idx - lookback; i < idx; i++)
        {
            if (candles[i].IsBullish) bullishCount++;
        }
        return bullishCount >= 2;
    }
}
