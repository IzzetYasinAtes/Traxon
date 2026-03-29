using Traxon.CryptoTrader.Application.Abstractions;

namespace Traxon.CryptoTrader.Infrastructure.Calculators;

/// <summary>Half Kelly criterion ile pozisyon buyuklugu hesaplayici.</summary>
public sealed class PositionSizer : IPositionSizer
{
    private const decimal MinEdge             = 0.12m; // Analyst v4: dusuk kalite trade filtreleme (0.08 → 0.12)
    private const decimal MinEdgeLowVol      = 0.15m; // Analyst v4: 0.12 → 0.15
    private const decimal MaxPositionFraction = 0.02m;
    private const decimal KellyMultiplier     = 0.15m;

    /// <summary>
    /// Conservative Kelly criterion ile pozisyon buyuklugu hesaplar.
    /// f* = (fairValue - marketPrice) / (1 - marketPrice) [UP icin]
    /// bet_size = f* * 0.15 * bankroll, max %2 bankroll (~$200 on $10k)
    /// </summary>
    public PositionSizeResult Calculate(decimal fairValue, decimal marketPrice, decimal bankroll, bool isLowVolatility = false)
    {
        var edge = Math.Abs(fairValue - marketPrice);
        var effectiveMinEdge = isLowVolatility ? MinEdgeLowVol : MinEdge;

        if (edge < effectiveMinEdge || bankroll <= 0m)
            return new PositionSizeResult(0m, 0m, edge, false);

        decimal kellyFull;
        if (fairValue > marketPrice)
            kellyFull = marketPrice > 0.9999m ? 0m : (fairValue - marketPrice) / (1m - marketPrice);
        else
            kellyFull = marketPrice < 0.0001m ? 0m : (marketPrice - fairValue) / marketPrice;

        var kellyFraction = kellyFull * KellyMultiplier;
        var maxPosition   = bankroll * MaxPositionFraction;
        var positionSize  = Math.Min(kellyFraction * bankroll, maxPosition);
        positionSize      = Math.Max(0m, positionSize);

        return new PositionSizeResult(
            Math.Round(kellyFraction, 6),
            Math.Round(positionSize, 2),
            Math.Round(edge, 6),
            true);
    }
}
