namespace LedgerLite.Domain.Common;

/// <summary>
/// Base class for entities. Collects domain events which are pulled (and cleared)
/// by infrastructure after a successful unit of work.
/// </summary>
public abstract class Entity
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public Guid Id { get; protected init; } = Guid.CreateVersion7();

    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    /// <summary>Returns all raised events and clears the collection (call after saving).</summary>
    public IReadOnlyList<IDomainEvent> PullEvents()
    {
        var events = _domainEvents.ToArray();
        _domainEvents.Clear();
        return events;
    }
}
