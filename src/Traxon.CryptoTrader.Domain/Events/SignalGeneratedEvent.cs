using Traxon.CryptoTrader.Domain.Abstractions;
using Traxon.CryptoTrader.Domain.Trading;

namespace Traxon.CryptoTrader.Domain.Events;

public sealed record SignalGeneratedEvent(Signal Signal) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
