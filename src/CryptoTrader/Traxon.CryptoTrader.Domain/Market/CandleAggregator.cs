using Traxon.CryptoTrader.Domain.Assets;

namespace Traxon.CryptoTrader.Domain.Market;

/// <summary>
/// Pure static utility: aggregates 1m candles into higher timeframe candles
/// and checks boundary completion.
/// </summary>
public static class CandleAggregator
{
    /// <summary>Checks if a 1m candle completes a higher timeframe boundary.</summary>
    public static bool CompletesTimeFrame(Candle oneMinuteCandle, TimeFrame targetTimeFrame)
    {
        var closeMinute = oneMinuteCandle.CloseTime.Minute;
        return targetTimeFrame.TotalSeconds switch
        {
            300  => (closeMinute + 1) % 5 == 0,   // minutes 4, 9, 14, 19, 24, 29...
            900  => (closeMinute + 1) % 15 == 0,  // minutes 14, 29, 44, 59
            3600 => closeMinute == 59,             // minute 59
            _    => false
        };
    }

    /// <summary>Aggregates 1m candles into a single higher-timeframe candle.</summary>
    public static Candle? Aggregate(IReadOnlyList<Candle> candles, Asset asset, TimeFrame targetTimeFrame)
    {
        if (candles.Count == 0) return null;

        var first = candles[0];
        var last  = candles[^1];

        decimal high        = decimal.MinValue;
        decimal low         = decimal.MaxValue;
        decimal volume      = 0m;
        decimal quoteVolume = 0m;
        int     tradeCount  = 0;

        foreach (var c in candles)
        {
            if (c.High > high) high = c.High;
            if (c.Low < low)   low  = c.Low;
            volume      += c.Volume;
            quoteVolume += c.QuoteVolume;
            tradeCount  += c.TradeCount;
        }

        return new Candle(
            id:          GenerateCandleId(asset.Symbol, targetTimeFrame.Value, first.OpenTime.Ticks),
            asset:       asset,
            timeFrame:   targetTimeFrame,
            openTime:    first.OpenTime,
            closeTime:   last.CloseTime,
            open:        first.Open,
            high:        high,
            low:         low,
            close:       last.Close,
            volume:      volume,
            quoteVolume: quoteVolume,
            tradeCount:  tradeCount,
            isClosed:    true);
    }

    /// <summary>Aggregates all 1m candles into higher timeframe candles (for backfill).</summary>
    public static IReadOnlyList<Candle> AggregateAll(
        IReadOnlyList<Candle> oneMinuteCandles,
        Asset asset,
        TimeFrame targetTimeFrame)
    {
        if (oneMinuteCandles.Count == 0)
            return [];

        var windowMinutes = targetTimeFrame.TotalSeconds / 60;
        var result = new List<Candle>();
        var bucket = new List<Candle>();

        foreach (var candle in oneMinuteCandles)
        {
            bucket.Add(candle);

            if (CompletesTimeFrame(candle, targetTimeFrame) && bucket.Count > 0)
            {
                var aggregated = Aggregate(bucket, asset, targetTimeFrame);
                if (aggregated is not null)
                    result.Add(aggregated);
                bucket.Clear();
            }
        }

        return result;
    }

    /// <summary>
    /// Generates a deterministic candle ID from symbol + interval + openTime.
    /// Same logic as BinanceMapper to prevent ID collisions.
    /// </summary>
    private static long GenerateCandleId(string symbol, string interval, long openTimeTicks)
    {
        long hash = openTimeTicks;
        foreach (var c in symbol)
            hash = hash * 31 + c;
        foreach (var c in interval)
            hash = hash * 31 + c;
        return hash;
    }
}
