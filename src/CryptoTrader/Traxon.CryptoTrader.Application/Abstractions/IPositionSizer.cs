namespace Traxon.CryptoTrader.Application.Abstractions;

public record PositionSizeResult(
    decimal KellyFraction,
    decimal PositionSize,
    decimal Edge,
    bool MeetsMinimumEdge);

public interface IPositionSizer
{
    /// <summary>
    /// Half Kelly criterion ile pozisyon buyuklugu hesaplar.
    /// f* = (fairValue - marketPrice) / (1 - marketPrice) [UP icin]
    /// bet_size = (f* / 2) * bankroll, max %5 bankroll
    /// </summary>
    PositionSizeResult Calculate(
        decimal fairValue,
        decimal marketPrice,
        decimal bankroll,
        bool isLowVolatility = false);
}
