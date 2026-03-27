using Traxon.CryptoTrader.Application.Abstractions;

namespace Traxon.CryptoTrader.Infrastructure.Calculators;

/// <summary>Half Kelly criterion ile pozisyon buyuklugu hesaplayici.</summary>
public sealed class PositionSizer : IPositionSizer
{
    private const decimal MinEdge             = 0.03m;
    private const decimal MaxPositionFraction = 0.05m;

    /// <summary>
    /// Half Kelly criterion ile pozisyon buyuklugu hesaplar.
    /// f* = (fairValue - marketPrice) / (1 - marketPrice) [UP icin]
    /// bet_size = (f* / 2) * bankroll, max %5 bankroll
    /// </summary>
    public PositionSizeResult Calculate(decimal fairValue, decimal marketPrice, decimal bankroll)
    {
        var edge = Math.Abs(fairValue - marketPrice);

        if (edge < MinEdge || bankroll <= 0m)
            return new PositionSizeResult(0m, 0m, edge, false);

        decimal kellyFull;
        if (fairValue > marketPrice)
            kellyFull = marketPrice > 0.9999m ? 0m : (fairValue - marketPrice) / (1m - marketPrice);
        else
            kellyFull = marketPrice < 0.0001m ? 0m : (marketPrice - fairValue) / marketPrice;

        var halfKelly    = kellyFull / 2m;
        var maxPosition  = bankroll * MaxPositionFraction;
        var positionSize = Math.Min(halfKelly * bankroll, maxPosition);
        positionSize     = Math.Max(0m, positionSize);

        return new PositionSizeResult(
            Math.Round(halfKelly, 6),
            Math.Round(positionSize, 2),
            Math.Round(edge, 6),
            true);
    }
}
