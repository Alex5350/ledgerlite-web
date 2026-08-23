using LedgerLite.Application.Abstractions;
using LedgerLite.Domain.Accounts;
using LedgerLite.Domain.Budgets;
using LedgerLite.Domain.FiscalPeriods;
using LedgerLite.Domain.Journal;
using LedgerLite.Domain.Users;
using LedgerLite.Domain.ValueObjects;
using LedgerLite.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LedgerLite.Infrastructure;

/// <summary>
/// Applies migrations and, when the database is empty, seeds a small demo dataset.
/// Intended to be called at API startup in Development only.
/// </summary>
public static class DatabaseInitialization
{
    public static async Task InitializeAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider;
        var logger = provider.GetRequiredService<ILoggerFactory>()
            .CreateLogger("LedgerLite.DatabaseInitialization");

        var context = provider.GetRequiredService<LedgerLiteDbContext>();

        if (context.Database.GetMigrations().Any())
        {
            await context.Database.MigrateAsync(cancellationToken);
        }
        else
        {
            // Fallback when no migration has been generated.
            await context.Database.EnsureCreatedAsync(cancellationToken);
        }

        if (await context.Users.AnyAsync(cancellationToken))
        {
            logger.LogInformation("Database already seeded; skipping demo data");
            return;
        }

        logger.LogInformation("Seeding demo data");

        var hasher = provider.GetRequiredService<IPasswordHasher>();
        SeedDemoData(context, hasher);
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Demo data seeded (demo user: demo@ledgerlite.io)");
    }

    private static void SeedDemoData(LedgerLiteDbContext context, IPasswordHasher hasher)
    {
        var period = FiscalPeriod.TryCreate(
            name: "January 2026",
            startDate: new DateOnly(2026, 1, 1),
            endDate: new DateOnly(2026, 1, 31),
            out var seededPeriod,
            out _)
            ? seededPeriod
            : throw new InvalidOperationException("Could not create the demo fiscal period.");

        context.FiscalPeriods.Add(period);

        context.Users.Add(User.Create(
            email: EmailAddress.Create("demo@ledgerlite.io"),
            displayName: "Demo User",
            passwordHash: hasher.Hash("Demo123!")));

        // Chart of accounts: Cash 1010, Bank 1020, Equity 3010, Salary 4010, Groceries 5010, Rent 5020.
        var accounts = new (string Number, string Name, AccountType Type)[]
        {
            ("1010", "Cash", AccountType.Asset),
            ("1020", "Bank", AccountType.Asset),
            ("3010", "Equity", AccountType.Equity),
            ("4010", "Salary", AccountType.Revenue),
            ("5010", "Groceries", AccountType.Expense),
            ("5020", "Rent", AccountType.Expense)
        };

        var accountsById = new Dictionary<(string, AccountType), Account>();
        foreach (var (number, name, type) in accounts)
        {
            if (!Account.TryCreate(AccountNumber.Create(number), name, type, period.Id, out var account, out var error))
            {
                throw new InvalidOperationException($"Could not seed account {number}: {error}");
            }

            context.Accounts.Add(account);
            accountsById[(number, type)] = account;
        }

        var bank = accountsById[("1020", AccountType.Asset)];
        var cash = accountsById[("1010", AccountType.Asset)];
        var equity = accountsById[("3010", AccountType.Equity)];
        var groceries = accountsById[("5010", AccountType.Expense)];

        AddPostedEntry(
            context,
            period,
            description: "Opening balances",
            occurredOnUtc: new DateTime(2026, 1, 2, 9, 0, 0, DateTimeKind.Utc),
            (bank.Id, 5000m, 0m),
            (equity.Id, 0m, 5000m));

        AddPostedEntry(
            context,
            period,
            description: "Weekly grocery run",
            occurredOnUtc: new DateTime(2026, 1, 10, 16, 30, 0, DateTimeKind.Utc),
            (groceries.Id, 120m, 0m),
            (cash.Id, 0m, 120m));

        if (!Budget.TryCreate(period.Id, "Groceries", Money.Create(500m, Money.DefaultCurrency), out var budget, out var budgetError))
        {
            throw new InvalidOperationException($"Could not seed demo budget: {budgetError}");
        }

        context.Budgets.Add(budget);
    }

    private static void AddPostedEntry(
        LedgerLiteDbContext context,
        FiscalPeriod period,
        string description,
        DateTime occurredOnUtc,
        params (Guid AccountId, decimal Debit, decimal Credit)[] lines)
    {
        if (!JournalEntry.TryCreate(
                period.Id,
                description,
                occurredOnUtc,
                lines.Select(l => new JournalEntryLineInput(l.AccountId, l.Debit, l.Credit)),
                out var entry,
                out var error))
        {
            throw new InvalidOperationException($"Could not seed journal entry '{description}': {error}");
        }

        if (!entry.TryPost(period, out var postError))
        {
            throw new InvalidOperationException($"Could not post journal entry '{description}': {postError}");
        }

        context.JournalEntries.Add(entry);
    }
}
