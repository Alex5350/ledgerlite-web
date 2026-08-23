using LedgerLite.Domain.Budgets;

namespace LedgerLite.Application.Abstractions;

public interface IBudgetRepository
{
    Task<Budget?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Budget>> GetByPeriodAsync(Guid fiscalPeriodId, CancellationToken cancellationToken = default);

    Task<Budget?> GetByCategoryAsync(Guid fiscalPeriodId, string category, CancellationToken cancellationToken = default);

    Task AddAsync(Budget budget, CancellationToken cancellationToken = default);
}
