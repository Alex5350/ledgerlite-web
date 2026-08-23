using System.Diagnostics.CodeAnalysis;
using LedgerLite.Domain.Common;
using LedgerLite.Domain.ValueObjects;

namespace LedgerLite.Domain.Accounts;

/// <summary>
/// A ledger account within a fiscal period. Uniqueness of the account number among
/// siblings of the same period is enforced by <see cref="Services.IAccountNumberUniquenessChecker"/>.
/// </summary>
public sealed class Account : Entity
{
    private Account(AccountNumber number, string name, AccountType type, Guid fiscalPeriodId)
    {
        Number = number;
        Name = name;
        Type = type;
        FiscalPeriodId = fiscalPeriodId;
    }

    public AccountNumber Number { get; }

    public string Name { get; }

    public AccountType Type { get; }

    public Guid FiscalPeriodId { get; }

    public static bool TryCreate(
        AccountNumber number,
        string? name,
        AccountType type,
        Guid fiscalPeriodId,
        [NotNullWhen(true)] out Account? account,
        [NotNullWhen(false)] out string? error)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            account = null;
            error = "Account name is required.";
            return false;
        }

        if (fiscalPeriodId == Guid.Empty)
        {
            account = null;
            error = "Account must belong to a fiscal period.";
            return false;
        }

        if (!Enum.IsDefined(type))
        {
            account = null;
            error = $"Account type '{type}' is not valid.";
            return false;
        }

        account = new Account(number, name.Trim(), type, fiscalPeriodId);
        error = null;
        return true;
    }
}
