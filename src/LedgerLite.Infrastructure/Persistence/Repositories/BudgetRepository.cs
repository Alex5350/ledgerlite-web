using LedgerLite.Application.Abstractions;
using LedgerLite.Domain.Budgets;
using LedgerLite.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LedgerLite.Infrastructure.Persistence.Repositories;

internal sealed class BudgetRepository(LedgerLiteDbContext context) : IBudgetRepository
{
    public Task<Budget?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Budgets.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Budget>> GetByPeriodAsync(
        Guid fiscalPeriodId,
        CancellationToken cancellationToken = default) =>
        // Tracked on purpose: EvaluateBudgetsCommand mutates NotifiedThresholds on these
        // aggregates and relies on IUnitOfWork.SaveChangesAsync persisting the change.
        await context.Budgets
            .Where(b => b.FiscalPeriodId == fiscalPeriodId)
            .OrderBy(b => b.Category)
            .ToListAsync(cancellationToken);

    public Task<Budget?> GetByCategoryAsync(
        Guid fiscalPeriodId,
        string category,
        CancellationToken cancellationToken = default) =>
        context.Budgets.FirstOrDefaultAsync(
            b => b.FiscalPeriodId == fiscalPeriodId && b.Category == category.Trim(),
            cancellationToken);

    public async Task AddAsync(Budget budget, CancellationToken cancellationToken = default) =>
        await context.Budgets.AddAsync(budget, cancellationToken);
}
