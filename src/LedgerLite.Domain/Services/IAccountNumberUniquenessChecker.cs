using LedgerLite.Domain.ValueObjects;

namespace LedgerLite.Domain.Services;

/// <summary>
/// Domain service: an account number must be unique within its fiscal period.
/// Implemented by infrastructure/application (backed by the account repository).
/// </summary>
public interface IAccountNumberUniquenessChecker
{
    Task<bool> IsUniqueAsync(AccountNumber number, Guid fiscalPeriodId, CancellationToken cancellationToken = default);
}
