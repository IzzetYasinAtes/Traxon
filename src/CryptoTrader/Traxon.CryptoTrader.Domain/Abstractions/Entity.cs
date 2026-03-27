namespace Traxon.CryptoTrader.Domain.Abstractions;

public abstract class Entity<TId>
{
    public TId Id { get; protected set; } = default!;

    private readonly List<IDomainEvent> _domainEvents = [];
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void AddDomainEvent(IDomainEvent domainEvent) =>
        _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();
}
