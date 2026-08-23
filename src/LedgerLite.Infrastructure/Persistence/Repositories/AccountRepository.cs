using LedgerLite.Application.Abstractions;
using LedgerLite.Domain.Accounts;
using LedgerLite.Domain.Specifications;
using LedgerLite.Domain.ValueObjects;
using LedgerLite.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LedgerLite.Infrastructure.Persistence.Repositories;

internal sealed class AccountRepository(LedgerLiteDbContext context) : IAccountRepository
{
    public Task<Account?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Accounts.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public Task<bool> NumberExistsInPeriodAsync(
        AccountNumber number,
        Guid fiscalPeriodId,
        CancellationToken cancellationToken = default) =>
        context.Accounts.AnyAsync(
            a => a.FiscalPeriodId == fiscalPeriodId && a.Number == number,
            cancellationToken);

    public async Task<IReadOnlyList<Account>> GetByPeriodAsync(
        Guid fiscalPeriodId,
        CancellationToken cancellationToken = default) =>
        await context.Accounts
            .AsNoTracking()
            .Where(a => a.FiscalPeriodId == fiscalPeriodId)
            .OrderBy(a => a.Number)
            .ThenBy(a => a.Name)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Account>> GetSatisfyingAsync(
        ISpecification<Account> specification,
        CancellationToken cancellationToken = default) =>
        await context.Accounts
            .AsNoTracking()
            .Where(specification.ToExpression())
            .ToListAsync(cancellationToken);

    public async Task AddAsync(Account account, CancellationToken cancellationToken = default) =>
        await context.Accounts.AddAsync(account, cancellationToken);
}
