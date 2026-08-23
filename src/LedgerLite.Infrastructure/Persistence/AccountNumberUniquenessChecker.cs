using LedgerLite.Application.Abstractions;
using LedgerLite.Domain.Services;
using LedgerLite.Domain.ValueObjects;

namespace LedgerLite.Infrastructure.Persistence;

/// <summary>Domain service backed by the account repository (unique number per fiscal period).</summary>
internal sealed class AccountNumberUniquenessChecker(IAccountRepository accounts)
    : IAccountNumberUniquenessChecker
{
    public async Task<bool> IsUniqueAsync(
        AccountNumber number,
        Guid fiscalPeriodId,
        CancellationToken cancellationToken = default) =>
        !await accounts.NumberExistsInPeriodAsync(number, fiscalPeriodId, cancellationToken);
}
