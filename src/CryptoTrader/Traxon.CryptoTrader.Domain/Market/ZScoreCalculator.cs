namespace Traxon.CryptoTrader.Domain.Market;

/// <summary>
/// Computes Z-score of recent 5-minute returns from 1m candles.
/// Used by the worker to determine preliminary mean-reversion direction.
/// </summary>
public static class ZScoreCalculator
{
    private const int WindowMinutes = 120;
    private const int ReturnPeriodMinutes = 5;

    /// <summary>
    /// Computes the Z-score of the current 5m return relative to rolling history.
    /// Positive Z = overbought, negative Z = oversold.
    /// </summary>
    public static decimal Compute(IReadOnlyList<Candle> oneMinuteCandles)
    {
        if (oneMinuteCandles.Count < ReturnPeriodMinutes + 1)
            return 0m;

        var recentCandles = oneMinuteCandles.Count > WindowMinutes
            ? oneMinuteCandles.Skip(oneMinuteCandles.Count - WindowMinutes).ToList()
            : oneMinuteCandles.ToList();

        // Group into 5-candle blocks and compute returns
        var returns = new List<decimal>();
        for (var i = 0; i + ReturnPeriodMinutes <= recentCandles.Count; i += ReturnPeriodMinutes)
        {
            var blockOpen  = recentCandles[i].Open;
            var blockClose = recentCandles[i + ReturnPeriodMinutes - 1].Close;
            if (blockOpen > 0)
                returns.Add((blockClose - blockOpen) / blockOpen);
        }

        if (returns.Count < 3)
            return 0m;

        var mu    = returns.Average();
        var sigma = StandardDeviation(returns, mu);

        if (sigma < 0.000001m)
            return 0m;

        // Current 5m return: last close vs close 5 minutes ago
        var latest       = recentCandles[^1].Close;
        var fiveMinAgo   = recentCandles.Count >= ReturnPeriodMinutes + 1
            ? recentCandles[^(ReturnPeriodMinutes + 1)].Close
            : recentCandles[0].Close;
        var currentReturn = fiveMinAgo > 0 ? (latest - fiveMinAgo) / fiveMinAgo : 0m;

        return (currentReturn - mu) / sigma;
    }

    private static decimal StandardDeviation(List<decimal> values, decimal mean)
    {
        var sumSquares = values.Sum(v => (v - mean) * (v - mean));
        return (decimal)Math.Sqrt((double)(sumSquares / values.Count));
    }
}
