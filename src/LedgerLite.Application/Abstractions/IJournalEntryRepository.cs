using LedgerLite.Domain.Journal;

namespace LedgerLite.Application.Abstractions;

public interface IJournalEntryRepository
{
    Task<JournalEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task AddAsync(JournalEntry entry, CancellationToken cancellationToken = default);

    /// <summary>Paged listing, optionally filtered by fiscal period. Returns items plus total count.</summary>
    Task<(IReadOnlyList<JournalEntry> Items, int TotalCount)> GetPagedAsync(
        Guid? fiscalPeriodId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>All posted entry lines of a period, used for trial balance and budget evaluation.</summary>
    Task<IReadOnlyList<(Guid AccountId, decimal Debit, decimal Credit)>> GetPostedLinesAsync(
        Guid fiscalPeriodId,
        CancellationToken cancellationToken = default);
}
