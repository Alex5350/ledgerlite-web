namespace LedgerLite.Application.Abstractions;

/// <summary>Commits all changes tracked by the repositories in the current scope.</summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>Dispatches domain events pulled from aggregates after a successful save.</summary>
public interface IDomainEventDispatcher
{
    Task DispatchAsync(IEnumerable<Domain.Common.IDomainEvent> domainEvents, CancellationToken cancellationToken = default);
}

/// <summary>Hashes and verifies passwords; implemented by infrastructure.</summary>
public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string hash);
}
