using LedgerLite.Application.Abstractions;
using LedgerLite.Domain.FiscalPeriods;
using LedgerLite.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LedgerLite.Infrastructure.Persistence.Repositories;

internal sealed class FiscalPeriodRepository(LedgerLiteDbContext context) : IFiscalPeriodRepository
{
    public Task<FiscalPeriod?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.FiscalPeriods.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<IReadOnlyList<FiscalPeriod>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await context.FiscalPeriods
            .AsNoTracking()
            .OrderByDescending(p => p.StartDate)
            .ThenBy(p => p.Name)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(FiscalPeriod period, CancellationToken cancellationToken = default) =>
        await context.FiscalPeriods.AddAsync(period, cancellationToken);
}
