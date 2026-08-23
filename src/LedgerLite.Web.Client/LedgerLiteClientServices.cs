using System.Net.Http;
using LedgerLite.Web.Client.Services.Api;
using LedgerLite.Web.Client.Services.Auth;
using LedgerLite.Web.Client.Ui;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LedgerLite.Web.Client;

/// <summary>
/// Registers every client-side service (API client, token store, auth state provider,
/// auth service) so that a single call works from BOTH Blazor hosts:
/// <code>
/// builder.Services.AddLedgerLiteClientServices(builder.Configuration);
/// builder.Services.AddAuthorization();
/// </code>
/// The API base URL comes from the "Api:BaseUrl" configuration key
/// (default <see cref="DefaultBaseUrl"/>, set in both appsettings.json files).
/// </summary>
public static class LedgerLiteClientServices
{
    public const string DefaultBaseUrl = "http://localhost:5080";

    public static IServiceCollection AddLedgerLiteClientServices(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var baseAddress = BuildBaseAddress(configuration["Api:BaseUrl"]);

        // The handler chain and HttpClient for ILedgerLiteApiClient are composed
        // per-scope instead of via IHttpClientFactory: the factory builds and caches
        // its chain from a root-level scope, which would capture a BearerTokenHandler
        // whose ITokenStore (and circuit-scoped IJSRuntime) belongs to no circuit —
        // breaking token attach on Interactive Server. A scoped primary handler means
        // each Blazor circuit (and the single WebAssembly scope) owns its complete
        // chain, and scope disposal tears down exactly that chain: a DelegatingHandler
        // disposes its inner handler when the scope ends, so sharing the primary
        // handler as a singleton would let one circuit's teardown kill every other
        // circuit's connection pool. On WebAssembly the scoped HttpClientHandler
        // routes through the browser fetch pipeline.
        services.TryAddScoped<HttpMessageHandler>(static _ => new HttpClientHandler());

        services.TryAddScoped<ITokenStore, LocalStorageTokenStore>();
        services.TryAddScoped<BearerTokenHandler>();
        services.TryAddScoped<JwtAuthenticationStateProvider>();
        services.TryAddScoped<AuthenticationStateProvider>(static serviceProvider =>
            serviceProvider.GetRequiredService<JwtAuthenticationStateProvider>());
        services.TryAddScoped<IAuthService, AuthService>();

        // UI state: selected fiscal period (topbar selector + pages) and toasts.
        services.TryAddScoped<PeriodState>();
        services.TryAddScoped<IToastService, ToastService>();
        services.TryAddScoped<ILedgerLiteApiClient>(serviceProvider =>
        {
            var bearerHandler = serviceProvider.GetRequiredService<BearerTokenHandler>();
            bearerHandler.InnerHandler = serviceProvider.GetRequiredService<HttpMessageHandler>();
            return new LedgerLiteApiClient(new HttpClient(bearerHandler, disposeHandler: false)
            {
                BaseAddress = baseAddress
            });
        });

        services.AddCascadingAuthenticationState();
        return services;
    }


    private static Uri BuildBaseAddress(string? baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            baseUrl = DefaultBaseUrl;
        }

        // Base addresses must end in '/' for relative request URIs to combine correctly.
        if (!baseUrl.EndsWith('/'))
        {
            baseUrl += "/";
        }

        return new Uri(baseUrl, UriKind.Absolute);
    }
}
