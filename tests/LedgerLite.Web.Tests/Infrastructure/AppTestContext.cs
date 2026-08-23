using System.Security.Claims;
using Bunit;
using Bunit.TestDoubles;
using LedgerLite.Web.Client.Services.Api;
using LedgerLite.Web.Client.Services.Auth;
using LedgerLite.Web.Client.Ui;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace LedgerLite.Web.Tests.Infrastructure;

/// <summary>
/// bUnit <see cref="TestContext"/> preconfigured with every client-side dependency the pages
/// and components rely on:
/// <list type="bullet">
/// <item>mocked <see cref="ILedgerLiteApiClient"/> and <see cref="IAuthService"/> (NSubstitute)</item>
/// <item>real <see cref="IToastService"/> implementation</item>
    /// <item>real <see cref="PeriodState"/> built on the mocked API client and bUnit's JS runtime
    /// (JS interop runs in Loose mode, so unconfigured localStorage calls return defaults)</item>
/// <item>a controllable <see cref="AuthenticationStateProvider"/> (default: logged-in principal)
/// plus the cascading authentication state required by AuthorizeView.</item>
/// </list>
/// </summary>
public sealed class AppTestContext : TestContext
{
    public AppTestContext()
    {
        Api = Substitute.For<ILedgerLiteApiClient>();
        Auth = Substitute.For<IAuthService>();
        Toast = new ToastService();
        AuthProvider = Substitute.For<AuthenticationStateProvider>();

        Services.AddSingleton(Api);
        Services.AddSingleton(Auth);
        Services.AddSingleton<IToastService>(Toast);
        Services.AddSingleton(AuthProvider);

        // AuthorizeView (Routes.razor, Login) authorizes against the default policy even
        // without Policy/Roles, and bunit swaps unregistered IAuthorizationService for a
        // throwing placeholder. Register bunit's fake: it succeeds for any principal.
        this.AddTestAuthorization();

        Periods = new PeriodState(Api, JSInterop.JSRuntime);
        Services.AddSingleton(Periods);

        // Unconfigured JS calls (localStorage reads/writes from PeriodState) return default
        // values instead of throwing bUnit's strict-mode JSRuntimeUnhandledInvocationException,
        // which mirrors "no stored selection" for pages rendered in tests.
        JSInterop.Mode = JSRuntimeMode.Loose;

        SetAuthenticationState(Authenticated("tester@ledgerlite.io"));
        RenderTree.TryAdd<CascadingAuthenticationState>();
    }

    /// <summary>Mocked typed API client used by pages and by <see cref="Periods"/>.</summary>
    public ILedgerLiteApiClient Api { get; }

    /// <summary>Mocked auth service used by the Login page.</summary>
    public IAuthService Auth { get; }

    /// <summary>Real toast service, so toast behaviour is exercised end to end.</summary>
    public IToastService Toast { get; }

    /// <summary>Real period selection state backed by <see cref="Api"/>.</summary>
    public PeriodState Periods { get; }

    /// <summary>Mocked authentication state provider; configure via <see cref="SetAuthenticationState"/>.</summary>
    public AuthenticationStateProvider AuthProvider { get; }

    /// <summary>Points the mocked provider at the given principal (null = anonymous user).</summary>
    public void SetAuthenticationState(ClaimsPrincipal? principal)
    {
        var state = new AuthenticationState(principal ?? Anonymous());
        AuthProvider.GetAuthenticationStateAsync().Returns(Task.FromResult(state));
    }

    /// <summary>An authenticated principal with the given email and name identifier.</summary>
    public static ClaimsPrincipal Authenticated(string email, string userId = "00000000-0000-0000-0000-000000000001")
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Name, email),
            new Claim(ClaimTypes.Email, email),
        ],
        authenticationType: "Bearer");

        return new ClaimsPrincipal(identity);
    }

    /// <summary>An unauthenticated principal, as seen before login.</summary>
    public static ClaimsPrincipal Anonymous() => new(new ClaimsIdentity());
}
