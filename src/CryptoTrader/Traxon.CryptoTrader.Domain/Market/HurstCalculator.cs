namespace Traxon.CryptoTrader.Domain.Market;

/// <summary>
/// Computes the Hurst exponent via the Rescaled Range (R/S) method.
/// H &lt; 0.45 = mean reverting, H &gt; 0.55 = trending, 0.45–0.55 = random walk.
/// </summary>
public static class HurstCalculator
{
    private static readonly int[] SubWindowSizes = [10, 20, 30, 60];

    /// <summary>
    /// Computes rolling Hurst exponent from 1m candle log-returns.
    /// Returns 0.50 (random walk) when data is insufficient or computation fails.
    /// </summary>
    public static decimal Compute(IReadOnlyList<Candle> candles, int windowSize = 120)
    {
        if (candles.Count < 2)
            return 0.50m;

        // Take last windowSize candles
        var startIdx = Math.Max(0, candles.Count - windowSize);
        var count = candles.Count - startIdx;

        if (count < SubWindowSizes[0] + 1)
            return 0.50m;

        // Compute log returns
        var returns = new double[count - 1];
        for (var i = 0; i < returns.Length; i++)
        {
            var prev = (double)candles[startIdx + i].Close;
            var curr = (double)candles[startIdx + i + 1].Close;
            if (prev <= 0) prev = 1e-10;
            if (curr <= 0) curr = 1e-10;
            returns[i] = Math.Log(curr / prev);
        }

        // For each sub-window size, compute average R/S
        var logN = new List<double>();
        var logRS = new List<double>();

        foreach (var n in SubWindowSizes)
        {
            if (n > returns.Length)
                continue;

            var chunkCount = returns.Length / n;
            if (chunkCount == 0)
                continue;

            var rsValues = new List<double>();

            for (var c = 0; c < chunkCount; c++)
            {
                var offset = c * n;

                // Mean of this chunk
                var mean = 0.0;
                for (var i = 0; i < n; i++)
                    mean += returns[offset + i];
                mean /= n;

                // Mean-adjusted cumulative deviation
                var cumulative = new double[n];
                cumulative[0] = returns[offset] - mean;
                for (var i = 1; i < n; i++)
                    cumulative[i] = cumulative[i - 1] + (returns[offset + i] - mean);

                var maxCum = cumulative[0];
                var minCum = cumulative[0];
                for (var i = 1; i < n; i++)
                {
                    if (cumulative[i] > maxCum) maxCum = cumulative[i];
                    if (cumulative[i] < minCum) minCum = cumulative[i];
                }

                var range = maxCum - minCum;

                // Standard deviation of returns in this chunk
                var sumSq = 0.0;
                for (var i = 0; i < n; i++)
                {
                    var diff = returns[offset + i] - mean;
                    sumSq += diff * diff;
                }
                var stdev = Math.Sqrt(sumSq / n);

                if (stdev < 1e-15)
                    continue;

                rsValues.Add(range / stdev);
            }

            if (rsValues.Count == 0)
                continue;

            var avgRS = 0.0;
            foreach (var v in rsValues) avgRS += v;
            avgRS /= rsValues.Count;

            if (avgRS <= 0)
                continue;

            logN.Add(Math.Log(n));
            logRS.Add(Math.Log(avgRS));
        }

        if (logN.Count < 2)
            return 0.50m;

        // Linear regression: logRS = slope * logN + intercept
        var slope = LinearRegressionSlope(logN, logRS);

        // Clamp to [0, 1]
        var hurst = Math.Max(0.0, Math.Min(1.0, slope));

        return (decimal)hurst;
    }

    private static double LinearRegressionSlope(List<double> x, List<double> y)
    {
        var n = x.Count;
        var sumX = 0.0;
        var sumY = 0.0;
        var sumXY = 0.0;
        var sumXX = 0.0;

        for (var i = 0; i < n; i++)
        {
            sumX += x[i];
            sumY += y[i];
            sumXY += x[i] * y[i];
            sumXX += x[i] * x[i];
        }

        var denominator = n * sumXX - sumX * sumX;
        if (Math.Abs(denominator) < 1e-15)
            return 0.50;

        return (n * sumXY - sumX * sumY) / denominator;
    }
}
