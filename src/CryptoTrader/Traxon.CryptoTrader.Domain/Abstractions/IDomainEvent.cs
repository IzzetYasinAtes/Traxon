namespace Traxon.CryptoTrader.Domain.Abstractions;

public interface IDomainEvent
{
    Guid EventId { get; }
    DateTime OccurredAt { get; }
}
