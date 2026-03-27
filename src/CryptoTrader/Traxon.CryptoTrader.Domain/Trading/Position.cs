using Traxon.CryptoTrader.Domain.Abstractions;
using Traxon.CryptoTrader.Domain.Assets;

namespace Traxon.CryptoTrader.Domain.Trading;

public sealed class Position : Entity<Guid>
{
    public Asset Asset { get; }
    public TimeFrame TimeFrame { get; }
    public SignalDirection Direction { get; }
    public decimal EntryPrice { get; }
    public decimal PositionSize { get; }
    public decimal? StopLoss { get; }
    public decimal? TakeProfit { get; }
    public DateTime OpenedAt { get; }

    private Position() { Asset = null!; TimeFrame = null!; }

    public Position(
        Asset asset,
        TimeFrame timeFrame,
        SignalDirection direction,
        decimal entryPrice,
        decimal positionSize,
        decimal? stopLoss,
        decimal? takeProfit)
    {
        Id = Guid.NewGuid();
        Asset = asset;
        TimeFrame = timeFrame;
        Direction = direction;
        EntryPrice = entryPrice;
        PositionSize = positionSize;
        StopLoss = stopLoss;
        TakeProfit = takeProfit;
        OpenedAt = DateTime.UtcNow;
    }

    public decimal UnrealizedPnL(decimal currentPrice) =>
        Direction == SignalDirection.Up
            ? (currentPrice - EntryPrice) * PositionSize
            : (EntryPrice - currentPrice) * PositionSize;
}
