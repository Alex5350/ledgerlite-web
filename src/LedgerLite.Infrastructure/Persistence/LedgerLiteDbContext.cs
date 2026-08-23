using LedgerLite.Domain.Accounts;
using LedgerLite.Domain.Budgets;
using LedgerLite.Domain.FiscalPeriods;
using LedgerLite.Domain.Journal;
using LedgerLite.Domain.Users;
using LedgerLite.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace LedgerLite.Infrastructure.Persistence;

/// <summary>
/// EF Core mapping for the LedgerLite domain. Value objects are stored as columns
/// (<see cref="Domain.ValueObjects.Money"/> as Amount + Currency via a complex property,
/// <see cref="Domain.ValueObjects.AccountNumber"/> / <see cref="Domain.ValueObjects.EmailAddress"/>
/// as strings) and domain events are never persisted.
/// </summary>
public sealed class LedgerLiteDbContext(DbContextOptions<LedgerLiteDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<FiscalPeriod> FiscalPeriods => Set<FiscalPeriod>();

    public DbSet<Account> Accounts => Set<Account>();

    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();

    public DbSet<EntryLine> EntryLines => Set<EntryLine>();

    public DbSet<Budget> Budgets => Set<Budget>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LedgerLiteDbContext).Assembly);

        // Domain events are in-memory only.
        modelBuilder.Entity<User>().Ignore(e => e.DomainEvents);
        modelBuilder.Entity<FiscalPeriod>().Ignore(e => e.DomainEvents);
        modelBuilder.Entity<Account>().Ignore(e => e.DomainEvents);
        modelBuilder.Entity<JournalEntry>().Ignore(e => e.DomainEvents);
        modelBuilder.Entity<EntryLine>().Ignore(e => e.DomainEvents);
        modelBuilder.Entity<Budget>().Ignore(e => e.DomainEvents);

        // SQLite has no sequential GUID story; every Id is a client-generated Guid v7.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (entityType.FindProperty(nameof(Domain.Common.Entity.Id)) is { } idProperty)
            {
                idProperty.ValueGenerated = ValueGenerated.Never;
            }
        }
    }
}
