using LedgerLite.Domain.Budgets;
using LedgerLite.Domain.Common;
using LedgerLite.Domain.ValueObjects;

namespace LedgerLite.Domain.Events;

/// <summary>Raised when a budget's spending crosses a tracked threshold (80% or 100% of the limit).</summary>
public sealed record BudgetThresholdExceededDomainEvent(
    Guid BudgetId,
    Guid FiscalPeriodId,
    string Category,
    BudgetThreshold Threshold,
    Money Limit,
    Money Spent,
    DateTime RaisedAtUtc) : IDomainEvent
{
    public Guid EventId { get; } = Guid.CreateVersion7();

    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}
