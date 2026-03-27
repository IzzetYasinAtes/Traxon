namespace Traxon.CryptoTrader.Domain.Abstractions;

public abstract class AggregateRoot<TId> : Entity<TId>
{
    public int Version { get; protected set; }
}
