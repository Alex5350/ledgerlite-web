using System.Diagnostics.CodeAnalysis;
using LedgerLite.Domain.Common;

namespace LedgerLite.Domain.FiscalPeriods;

/// <summary>
/// An accounting period (e.g. 'January 2026'). Entries may only be posted while the period is open,
/// and a period cannot be closed before its end date has passed.
/// </summary>
public sealed class FiscalPeriod : Entity
{
    private FiscalPeriod(string name, DateOnly startDate, DateOnly endDate)
    {
        Name = name;
        StartDate = startDate;
        EndDate = endDate;
        Status = FiscalPeriodStatus.Open;
    }

    public string Name { get; }

    public DateOnly StartDate { get; }

    public DateOnly EndDate { get; }

    public FiscalPeriodStatus Status { get; private set; }

    public bool IsOpen => Status == FiscalPeriodStatus.Open;

    public static bool TryCreate(
        string? name,
        DateOnly startDate,
        DateOnly endDate,
        [NotNullWhen(true)] out FiscalPeriod? period,
        [NotNullWhen(false)] out string? error)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            period = null;
            error = "Fiscal period name is required.";
            return false;
        }

        if (endDate < startDate)
        {
            period = null;
            error = "Fiscal period end date must not be before its start date.";
            return false;
        }

        period = new FiscalPeriod(name.Trim(), startDate, endDate);
        error = null;
        return true;
    }

    /// <summary>Closes the period. Fails when already closed or before the end date has passed.</summary>
    public bool TryClose(DateOnly today, [NotNullWhen(false)] out string? error)
    {
        if (Status == FiscalPeriodStatus.Closed)
        {
            error = "Fiscal period is already closed.";
            return false;
        }

        if (today < EndDate)
        {
            error = "Fiscal period cannot be closed before its end date.";
            return false;
        }

        Status = FiscalPeriodStatus.Closed;
        error = null;
        return true;
    }
}
