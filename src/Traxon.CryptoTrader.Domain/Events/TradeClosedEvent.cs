using Traxon.CryptoTrader.Domain.Abstractions;
using Traxon.CryptoTrader.Domain.Trading;

namespace Traxon.CryptoTrader.Domain.Events;

public sealed record TradeClosedEvent(Trade Trade, string Outcome) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
