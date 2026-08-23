using LedgerLite.Domain.Accounts;
using LedgerLite.Domain.Specifications;
using LedgerLite.Domain.ValueObjects;

namespace LedgerLite.Application.Abstractions;

public interface IAccountRepository
{
    Task<Account?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> NumberExistsInPeriodAsync(AccountNumber number, Guid fiscalPeriodId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Account>> GetByPeriodAsync(Guid fiscalPeriodId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Account>> GetSatisfyingAsync(ISpecification<Account> specification, CancellationToken cancellationToken = default);

    Task AddAsync(Account account, CancellationToken cancellationToken = default);
}
