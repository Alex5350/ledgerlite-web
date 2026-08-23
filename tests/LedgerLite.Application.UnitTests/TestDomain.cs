using ErrorOr;
using LedgerLite.Domain.Accounts;
using LedgerLite.Domain.Budgets;
using LedgerLite.Domain.FiscalPeriods;
using LedgerLite.Domain.Journal;
using LedgerLite.Domain.Users;
using LedgerLite.Domain.ValueObjects;

namespace LedgerLite.Application.UnitTests;

/// <summary>Small builders for valid domain aggregates used across handler tests.</summary>
internal static class TestDomain
{
    public static FiscalPeriod OpenPeriod(DateOnly? start = null, DateOnly? end = null)
    {
        var ok = FiscalPeriod.TryCreate("Test period", start ?? new DateOnly(2026, 9, 1), end ?? new DateOnly(2026, 9, 30), out var period, out var error);
        return ok ? period! : throw new InvalidOperationException(error);
    }

    public static FiscalPeriod ClosedPeriod()
    {
        var period = OpenPeriod(start: new DateOnly(2026, 1, 1), end: new DateOnly(2026, 1, 31));
        if (!period.TryClose(new DateOnly(2026, 2, 1), out var error))
        {
            throw new InvalidOperationException(error);
        }

        return period;
    }

    public static Account NewAccount(string number = "1010", string name = "Cash", AccountType type = AccountType.Asset, Guid? periodId = null)
    {
        var ok = Account.TryCreate(AccountNumber.Create(number), name, type, periodId ?? Guid.CreateVersion7(), out var account, out var error);
        return ok ? account! : throw new InvalidOperationException(error);
    }

    public static JournalEntry NewEntry(Guid periodId, params (Guid AccountId, decimal Debit, decimal Credit)[] lines)
    {
        var ok = JournalEntry.TryCreate(
            periodId,
            "Test entry",
            new DateTime(2026, 9, 15, 10, 0, 0, DateTimeKind.Utc),
            lines.Select(l => new JournalEntryLineInput(l.AccountId, l.Debit, l.Credit)),
            out var entry,
            out var error);
        return ok ? entry! : throw new InvalidOperationException(error);
    }

    public static Budget NewBudget(Guid periodId, string category = "Groceries", decimal limit = 100m)
    {
        var ok = Budget.TryCreate(periodId, category, Money.Create(limit, "USD"), out var budget, out var error);
        return ok ? budget! : throw new InvalidOperationException(error);
    }

    public static User NewUser(string email = "jane@example.com")
    {
        var ok = EmailAddress.TryCreate(email, out var address, out var error);
        return ok ? User.Create(address!, "Jane Doe", "hash") : throw new InvalidOperationException(error);
    }
}

/// <summary>ErrorOr helpers for concise assertions.</summary>
internal static class ErrorOrTestExtensions
{
    public static Error FirstErrorOrThrow<T>(this ErrorOr<T> result)
    {
        Assert.True(result.IsError, $"Expected an error but the handler succeeded with '{result.Value}'.");
        return result.FirstError;
    }
}
