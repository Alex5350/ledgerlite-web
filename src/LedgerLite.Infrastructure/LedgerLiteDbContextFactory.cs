using LedgerLite.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LedgerLite.Infrastructure;

/// <summary>
/// Design-time factory so `dotnet ef` can create <see cref="LedgerLiteDbContext"/>
/// without booting the API host.
/// </summary>
public sealed class LedgerLiteDbContextFactory : IDesignTimeDbContextFactory<LedgerLiteDbContext>
{
    public LedgerLiteDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<LedgerLiteDbContext>()
            .UseSqlite("Data Source=ledgerlite.db")
            .Options;

        return new LedgerLiteDbContext(options);
    }
}
