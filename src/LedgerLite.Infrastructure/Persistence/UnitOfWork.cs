using LedgerLite.Application.Abstractions;
using LedgerLite.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LedgerLite.Infrastructure.Persistence;

/// <summary>Commits everything tracked by the scoped <see cref="LedgerLiteDbContext"/>.</summary>
internal sealed class UnitOfWork(LedgerLiteDbContext context) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
