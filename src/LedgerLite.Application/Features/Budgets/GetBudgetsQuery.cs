using ErrorOr;
using LedgerLite.Application.Abstractions;
using LedgerLite.Domain.Budgets;

namespace LedgerLite.Application.Features.Budgets;

public sealed record GetBudgetsQuery(Guid PeriodId);

public sealed record BudgetDto(Guid Id, Guid FiscalPeriodId, string Category, decimal LimitAmount, string Currency);

public sealed class GetBudgetsHandler(IBudgetRepository budgets)
    : IQueryHandler<GetBudgetsQuery, IReadOnlyList<BudgetDto>>
{
    public async Task<ErrorOr<IReadOnlyList<BudgetDto>>> Handle(
        GetBudgetsQuery query,
        CancellationToken cancellationToken = default)
    {
        var results = await budgets.GetByPeriodAsync(query.PeriodId, cancellationToken);

        return results
            .Select(b => new BudgetDto(
                Id: b.Id,
                FiscalPeriodId: b.FiscalPeriodId,
                Category: b.Category,
                LimitAmount: b.Limit.Amount,
                Currency: b.Limit.Currency))
            .ToList();
    }
}
