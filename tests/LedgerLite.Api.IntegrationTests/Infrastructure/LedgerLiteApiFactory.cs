using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LedgerLite.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Boots the real API in-process with:
/// - a unique temp SQLite database per factory instance (deleted on dispose),
/// - the Development environment so migrations run and demo data is seeded,
/// - the login/global rate limiters loosened so parallel test classes are not throttled.
/// </summary>
public class LedgerLiteApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath =
        Path.Combine(Path.GetTempPath(), $"ledgerlite-it-{Guid.NewGuid():N}.db");

    /// <summary>Subclasses (e.g. rate-limiting tests) can keep the production limiter.</summary>
    protected virtual bool LoosenRateLimiters => true;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:LedgerLite"] = $"Data Source={_databasePath}",
            // Deterministic JWT settings independent of the content root.
            ["Jwt:Issuer"] = "LedgerLite",
            ["Jwt:Audience"] = "LedgerLite.Api",
            ["Jwt:Key"] = "integration-test-signing-key-0123456789ABCDEF",
            ["Jwt:ExpiryMinutes"] = "60",
            // Keep the test output readable: Serilog reads these overrides from config.
            ["Serilog:MinimumLevel:Default"] = "Warning",
            ["Serilog:MinimumLevel:Override:Microsoft.AspNetCore"] = "Warning",
            ["Serilog:MinimumLevel:Override:Microsoft.EntityFrameworkCore"] = "Warning",
            ["Serilog:MinimumLevel:Override:Microsoft.EntityFrameworkCore.Database.Command"] = "Warning"
        }));

        if (LoosenRateLimiters)
        {
            builder.ConfigureServices(services =>
            {
                // Drop the API's strict limiter configuration (5/min login policy, 200/min global)
                // and replace it with a no-op limiter so parallel test classes are not throttled.
                foreach (var descriptor in services
                             .Where(d => d.ServiceType == typeof(IConfigureOptions<RateLimiterOptions>))
                             .ToList())
                {
                    services.Remove(descriptor);
                }

                services.PostConfigure<RateLimiterOptions>(options =>
                {
                    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                        RateLimitPartition.GetNoLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown"));
                    options.AddPolicy("auth-login", context =>
                        RateLimitPartition.GetNoLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown"));
                });
            });
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        DeleteDatabaseFiles();
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        DeleteDatabaseFiles();
    }

    private void DeleteDatabaseFiles()
    {
        foreach (var suffix in new[] { "", "-wal", "-shm" })
        {
            TryDelete(_databasePath + suffix);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Best effort cleanup of temp files; the OS temp dir is swept anyway.
        }
    }
}

/// <summary>Keeps the production 5/min login limiter for rate-limit assertions.</summary>
public sealed class RateLimitedApiFactory : LedgerLiteApiFactory
{
    protected override bool LoosenRateLimiters => false;
}
