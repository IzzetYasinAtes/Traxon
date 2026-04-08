namespace Traxon.CryptoTrader.Domain.Market;

/// <summary>
/// Computes taker buy/sell ratio from recent candles using a price-action proxy.
/// Ratio &gt; 0.55 = buyers aggressive (bullish), &lt; 0.45 = sellers aggressive (bearish).
/// Uses candle direction (close vs open) weighted by volume as proxy for taker flow.
/// </summary>
public static class TakerRatioCalculator
{
    /// <summary>
    /// Computes taker buy ratio from the last <paramref name="lookback"/> candles.
    /// Returns 0.50 (neutral) when data is insufficient or total volume is zero.
    /// </summary>
    public static decimal Compute(IReadOnlyList<Candle> candles, int lookback = 5)
    {
        if (candles.Count == 0)
            return 0.50m;

        var startIdx = Math.Max(0, candles.Count - lookback);
        var totalVolume = 0m;
        var buyVolume = 0m;

        for (var i = startIdx; i < candles.Count; i++)
        {
            totalVolume += candles[i].Volume;
            buyVolume += candles[i].TakerBuyBaseVolume;
        }

        if (totalVolume <= 0m)
            return 0.50m;

        return buyVolume / totalVolume;
    }
}
