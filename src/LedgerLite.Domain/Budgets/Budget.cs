using System.Diagnostics.CodeAnalysis;
using LedgerLite.Domain.Common;
using LedgerLite.Domain.Events;
using LedgerLite.Domain.ValueObjects;

namespace LedgerLite.Domain.Budgets;

/// <summary>Thresholds a budget can notify on; flags so already-notified levels are tracked.</summary>
[Flags]
public enum BudgetThreshold
{
    None = 0,
    EightyPercent = 1,
    HundredPercent = 2,
}

/// <summary>
/// A spending limit for a category within a fiscal period.
/// <see cref="EvaluateSpending"/> raises <see cref="BudgetThresholdExceededDomainEvent"/>
/// once when spending crosses 80% of the limit and once when it crosses 100%.
/// </summary>
public sealed class Budget : Entity
{
    private Budget(Guid fiscalPeriodId, string category, Money limit)
    {
        FiscalPeriodId = fiscalPeriodId;
        Category = category;
        Limit = limit;
    }

    /// <summary>
    /// Persistence-only constructor. EF Core cannot constructor-bind complex value objects
    /// (such as <see cref="Money"/> mapped to Amount + Currency columns), so hydrated
    /// aggregates are populated through their backing fields via this constructor.
    /// </summary>
    private Budget()
    {
        // EF Core populates this through the backing field; null-forgiving keeps the
        // non-nullable annotation honest without affecting domain construction.
        Category = null!;
    }

    public Guid FiscalPeriodId { get; }

    public string Category { get; }

    public Money Limit { get; private set; }

    /// <summary>Which threshold events have already been raised (prevents duplicate notifications).</summary>
    public BudgetThreshold NotifiedThresholds { get; private set; } = BudgetThreshold.None;

    public static bool TryCreate(
        Guid fiscalPeriodId,
        string? category,
        Money limit,
        [NotNullWhen(true)] out Budget? budget,
        [NotNullWhen(false)] out string? error)
    {
        if (fiscalPeriodId == Guid.Empty)
        {
            budget = null;
            error = "Budget must belong to a fiscal period.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(category))
        {
            budget = null;
            error = "Budget category is required.";
            return false;
        }

        if (!limit.IsPositive)
        {
            budget = null;
            error = "Budget limit must be greater than zero.";
            return false;
        }

        budget = new Budget(fiscalPeriodId, category.Trim(), limit);
        error = null;
        return true;
    }

    /// <summary>
    /// Compares spending against the limit and raises threshold events for newly crossed
    /// thresholds only. Called by the application layer after posting entries.
    /// </summary>
    public void EvaluateSpending(Money spentSoFar)
    {
        if (spentSoFar.Currency != Limit.Currency)
        {
            throw new InvalidOperationException(
                $"Spending currency ({spentSoFar.Currency}) does not match budget limit currency ({Limit.Currency}).");
        }

        var ratio = Limit.Amount == 0 ? 0 : spentSoFar.Amount / Limit.Amount;

        if (ratio >= 1m && !NotifiedThresholds.HasFlag(BudgetThreshold.HundredPercent))
        {
            NotifiedThresholds |= BudgetThreshold.HundredPercent | BudgetThreshold.EightyPercent;
            RaiseThresholdEvent(BudgetThreshold.HundredPercent, spentSoFar);
            return;
        }

        if (ratio >= 0.8m && !NotifiedThresholds.HasFlag(BudgetThreshold.EightyPercent))
        {
            NotifiedThresholds |= BudgetThreshold.EightyPercent;
            RaiseThresholdEvent(BudgetThreshold.EightyPercent, spentSoFar);
        }
    }

    private void RaiseThresholdEvent(BudgetThreshold threshold, Money spentSoFar) =>
        Raise(new BudgetThresholdExceededDomainEvent(
            BudgetId: Id,
            FiscalPeriodId: FiscalPeriodId,
            Category: Category,
            Threshold: threshold,
            Limit: Limit,
            Spent: spentSoFar,
            RaisedAtUtc: DateTime.UtcNow));
}
