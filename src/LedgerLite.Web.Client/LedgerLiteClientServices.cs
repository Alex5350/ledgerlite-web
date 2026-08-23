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

        // Shared primary handler: on WebAssembly this routes through the browser fetch
        // pipeline, on the server it is a regular sockets handler. A single shared
        // instance keeps connection pooling centralized.
        //
        // The HttpClient for ILedgerLiteApiClient is composed per-scope instead of via
        // IHttpClientFactory: the factory builds and caches its handler chain from a
        // root-level scope, which would capture a BearerTokenHandler whose ITokenStore
        // (and circuit-scoped IJSRuntime) belongs to no circuit — breaking token
        // refresh on Interactive Server. Per-scope composition gives each circuit
        // (and the single WebAssembly scope) a correctly wired chain.
        services.TryAddSingleton<HttpMessageHandler>(static _ => new HttpClientHandler());

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
