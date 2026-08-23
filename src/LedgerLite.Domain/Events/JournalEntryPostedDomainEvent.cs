using LedgerLite.Domain.Common;

namespace LedgerLite.Domain.Events;

/// <summary>Raised when a journal entry is successfully posted.</summary>
public sealed record JournalEntryPostedDomainEvent(
    Guid EntryId,
    Guid FiscalPeriodId,
    DateTime PostedAtUtc) : IDomainEvent
{
    public Guid EventId { get; } = Guid.CreateVersion7();

    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}
