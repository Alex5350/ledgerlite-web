namespace LedgerLite.Domain.Common;

/// <summary>A domain event raised by an aggregate and dispatched after persistence.</summary>
public interface IDomainEvent
{
    Guid EventId { get; }

    DateTime OccurredOnUtc { get; }
}
