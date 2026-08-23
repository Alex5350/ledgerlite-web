using LedgerLite.Application.Abstractions;
using LedgerLite.Domain.Journal;
using LedgerLite.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LedgerLite.Infrastructure.Persistence.Repositories;

internal sealed class JournalEntryRepository(LedgerLiteDbContext context) : IJournalEntryRepository
{
    public Task<JournalEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.JournalEntries
            .Include(e => e.Lines)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public async Task AddAsync(JournalEntry entry, CancellationToken cancellationToken = default) =>
        await context.JournalEntries.AddAsync(entry, cancellationToken);

    public async Task<(IReadOnlyList<JournalEntry> Items, int TotalCount)> GetPagedAsync(
        Guid? fiscalPeriodId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = context.JournalEntries
            .AsNoTracking()
            .Include(e => e.Lines)
            .AsQueryable();

        if (fiscalPeriodId is { } periodId)
        {
            query = query.Where(e => e.FiscalPeriodId == periodId);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(e => e.OccurredOn)
            .ThenBy(e => e.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<IReadOnlyList<(Guid AccountId, decimal Debit, decimal Credit)>> GetPostedLinesAsync(
        Guid fiscalPeriodId,
        CancellationToken cancellationToken = default)
    {
        var lines = await context.JournalEntries
            .AsNoTracking()
            .Where(e => e.FiscalPeriodId == fiscalPeriodId && e.IsPosted)
            .SelectMany(e => e.Lines)
            .Select(l => new { l.AccountId, l.Debit, l.Credit })
            .ToListAsync(cancellationToken);

        return [.. lines.Select(l => (l.AccountId, l.Debit, l.Credit))];
    }
}
