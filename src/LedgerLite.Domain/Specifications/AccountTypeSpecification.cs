using System.Linq.Expressions;
using LedgerLite.Domain.Accounts;

namespace LedgerLite.Domain.Specifications;

/// <summary>Matches accounts of the given type, optionally narrowed to a fiscal period.</summary>
public sealed class AccountTypeSpecification(
    AccountType accountType,
    Guid? fiscalPeriodId = null) : Specification<Account>
{
    public override Expression<Func<Account, bool>> ToExpression() =>
        fiscalPeriodId is { } periodId
            ? account => account.Type == accountType && account.FiscalPeriodId == periodId
            : account => account.Type == accountType;
}
