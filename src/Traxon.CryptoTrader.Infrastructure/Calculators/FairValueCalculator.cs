using MathNet.Numerics.Distributions;
using Traxon.CryptoTrader.Application.Abstractions;
using Traxon.CryptoTrader.Domain.Assets;
using Traxon.CryptoTrader.Domain.Market;

namespace Traxon.CryptoTrader.Infrastructure.Calculators;

/// <summary>
/// Black-Scholes d2 formulune dayali fair value hesaplayici.
/// P(Up) = Φ(d2) where d2 = (μ - σ²/2) / σ
/// </summary>
public sealed class FairValueCalculator : IFairValueCalculator
{
    /// <summary>Candle listesinden fair value hesaplar.</summary>
    public FairValueResult Calculate(IReadOnlyList<Candle> candles, TimeFrame timeFrame)
    {
        if (candles.Count < 14)
            return new FairValueResult(0.5m, 0m, 0m, 0m);

        var closes = candles.Select(c => c.Close).ToList();
        var mu = CalculateMomentum(closes, period: 12);
        var sigma = CalculateParkinsonPerCandle(candles, period: 14);

        if (sigma == 0m)
            return new FairValueResult(0.5m, mu, sigma, 0m);

        // d2 = (μ - σ²/2) / σ
        var d2 = (double)((mu - sigma * sigma / 2m) / sigma);

        var fairValue = (decimal)Normal.CDF(0, 1, d2);
        fairValue = Math.Max(0.01m, Math.Min(0.99m, fairValue));

        return new FairValueResult(
            Math.Round(fairValue, 6),
            Math.Round(mu, 10),
            Math.Round(sigma, 10),
            Math.Round((decimal)d2, 8));
    }

    /// <summary>EMA of log returns hesaplar (momentum proxy).</summary>
    public decimal CalculateMomentum(IReadOnlyList<decimal> closes, int period = 12)
    {
        if (closes.Count < period + 1)
            return 0m;

        var logReturns = new List<decimal>();
        for (int i = 1; i < closes.Count; i++)
        {
            if (closes[i - 1] <= 0) continue;
            logReturns.Add((decimal)Math.Log((double)(closes[i] / closes[i - 1])));
        }

        if (logReturns.Count < period)
            return 0m;

        var k = 2m / (period + 1);
        var ema = logReturns.Take(period).Average();
        for (int i = period; i < logReturns.Count; i++)
            ema = logReturns[i] * k + ema * (1m - k);

        return ema;
    }

    private static decimal CalculateParkinsonPerCandle(IReadOnlyList<Candle> candles, int period)
    {
        if (candles.Count < period) return 0m;

        var last = candles.TakeLast(period).ToList();
        var sumSquaredLogs = last.Sum(c =>
        {
            if (c.Low <= 0) return 0.0;
            var ratio = (double)(c.High / c.Low);
            var logRatio = Math.Log(ratio);
            return logRatio * logRatio;
        });

        return (decimal)Math.Sqrt(sumSquaredLogs / (4.0 * period * Math.Log(2.0)));
    }
}
