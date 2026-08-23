using System.Threading.Channels;
using LedgerLite.Application.Abstractions;
using LedgerLite.Domain.Events;
using LedgerLite.Domain.Services;
using LedgerLite.Infrastructure.Authentication;
using LedgerLite.Infrastructure.Messaging;
using LedgerLite.Infrastructure.Persistence;
using LedgerLite.Infrastructure.Persistence.Repositories;
using LedgerLite.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LedgerLite.Infrastructure;

public static class DependencyInjection
{
    public const string DefaultConnectionString = "Data Source=ledgerlite.db";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddPersistence(configuration)
            .AddAuthenticationServices()
            .AddDomainEventPipeline();

        return services;
    }

    private static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        // Resolved lazily inside the options lambda so configuration overrides applied by the host
        // (environment variables, integration-test factories) are honored when a DbContext is built.
        services.AddDbContext<LedgerLiteDbContext>(options =>
            options.UseSqlite(configuration.GetConnectionString("LedgerLite") ?? DefaultConnectionString));

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<IJournalEntryRepository, JournalEntryRepository>();
        services.AddScoped<IFiscalPeriodRepository, FiscalPeriodRepository>();
        services.AddScoped<IBudgetRepository, BudgetRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IAccountNumberUniquenessChecker, AccountNumberUniquenessChecker>();

        return services;
    }

    private static IServiceCollection AddAuthenticationServices(this IServiceCollection services)
    {
        services.AddOptions<JwtOptions>()
            .BindConfiguration(JwtOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();

        return services;
    }

    private static IServiceCollection AddDomainEventPipeline(this IServiceCollection services)
    {
        services.AddSingleton(_ =>
            Channel.CreateBounded<BudgetThresholdExceededDomainEvent>(new BoundedChannelOptions(capacity: 100)
            {
                FullMode = BoundedChannelFullMode.DropWrite,
                SingleReader = true,
                SingleWriter = false
            }));

        services.AddSingleton(static provider =>
            provider.GetRequiredService<Channel<BudgetThresholdExceededDomainEvent>>().Reader);
        services.AddSingleton(static provider =>
            provider.GetRequiredService<Channel<BudgetThresholdExceededDomainEvent>>().Writer);

        services.AddSingleton<IDomainEventDispatcher, ChannelDomainEventDispatcher>();
        services.AddHostedService<BudgetAlertWorker>();

        return services;
    }
}
