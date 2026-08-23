using LedgerLite.Application.Abstractions;
using LedgerLite.Domain.Users;
using LedgerLite.Domain.ValueObjects;
using LedgerLite.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LedgerLite.Infrastructure.Persistence.Repositories;

internal sealed class UserRepository(LedgerLiteDbContext context) : IUserRepository
{
    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public Task<User?> GetByEmailAsync(EmailAddress email, CancellationToken cancellationToken = default) =>
        context.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

    public Task<bool> EmailExistsAsync(EmailAddress email, CancellationToken cancellationToken = default) =>
        context.Users.AnyAsync(u => u.Email == email, cancellationToken);

    public async Task AddAsync(User user, CancellationToken cancellationToken = default) =>
        await context.Users.AddAsync(user, cancellationToken);
}
