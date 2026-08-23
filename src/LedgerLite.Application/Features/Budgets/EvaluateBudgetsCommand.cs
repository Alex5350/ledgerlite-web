using ErrorOr;
using LedgerLite.Application.Abstractions;
using LedgerLite.Application.Common;
using LedgerLite.Domain.Accounts;
using LedgerLite.Domain.Events;
using LedgerLite.Domain.Specifications;
using LedgerLite.Domain.ValueObjects;

namespace LedgerLite.Application.Features.Budgets;

public sealed record EvaluateBudgetsCommand(Guid PeriodId);

public sealed record BudgetEvaluationDto(
    Guid BudgetId,
    string Category,
    decimal LimitAmount,
    string Currency,
    decimal SpentAmount,
    IReadOnlyList<string> ThresholdsExceeded);

/// <summary>
/// Application service that re-evaluates every budget of a period against posted spending.
/// A budget's category matches the name of an expense account in the same period;
/// spending is the total posted debits of that account. Domain threshold events
/// (80% / 100% of the limit) are raised and dispatched here.
/// </summary>
public sealed class EvaluateBudgetsHandler(
    IBudgetRepository budgets,
    IJournalEntryRepository entries,
    IAccountRepository accounts,
    IUnitOfWork unitOfWork,
    IDomainEventDispatcher dispatcher) : ICommandHandler<EvaluateBudgetsCommand, IReadOnlyList<BudgetEvaluationDto>>
{
    public async Task<ErrorOr<IReadOnlyList<BudgetEvaluationDto>>> Handle(
        EvaluateBudgetsCommand command,
        CancellationToken cancellationToken = default)
    {
        var periodBudgets = await budgets.GetByPeriodAsync(command.PeriodId, cancellationToken);
        if (periodBudgets.Count == 0)
        {
            return DomainErrors.Budgets.NotFound;
        }

        var postedLines = await entries.GetPostedLinesAsync(command.PeriodId, cancellationToken);
        var postedDebitsByAccount = postedLines
            .GroupBy(line => line.AccountId)
            .ToDictionary(g => g.Key, g => g.Sum(line => line.Debit));

        var expenseAccounts = await accounts.GetSatisfyingAsync(
            new AccountTypeSpecification(AccountType.Expense, command.PeriodId),
            cancellationToken);

        var results = new List<BudgetEvaluationDto>();

        foreach (var budget in periodBudgets)
        {
            var account = expenseAccounts.FirstOrDefault(a =>
                string.Equals(a.Name, budget.Category, StringComparison.OrdinalIgnoreCase));

            var spent = account is not null
                ? postedDebitsByAccount.GetValueOrDefault(account.Id)
                : 0m;

            var eventsBefore = budget.DomainEvents.Count;
            budget.EvaluateSpending(Money.Create(spent, budget.Limit.Currency));
            var newEvents = budget.DomainEvents.Skip(eventsBefore)
                .OfType<BudgetThresholdExceededDomainEvent>()
                .Select(e => e.Threshold.ToString())
                .ToList();

            results.Add(new BudgetEvaluationDto(
                BudgetId: budget.Id,
                Category: budget.Category,
                LimitAmount: budget.Limit.Amount,
                Currency: budget.Limit.Currency,
                SpentAmount: spent,
                ThresholdsExceeded: newEvents));
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var allEvents = results.Count > 0
            ? periodBudgets.SelectMany(b => b.PullEvents()).ToList()
            : [];
        await dispatcher.DispatchAsync(allEvents, cancellationToken);

        return results;
    }
}
