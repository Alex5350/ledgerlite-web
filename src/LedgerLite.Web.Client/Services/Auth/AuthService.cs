using LedgerLite.Web.Client.Services.Api;

namespace LedgerLite.Web.Client.Services.Auth;

/// <summary>Outcome of a login or registration attempt; failures carry a user-presentable message.</summary>
public sealed record AuthResult(bool Success, string? Error)
{
    public static AuthResult Ok { get; } = new(Success: true, Error: null);

    public static AuthResult Fail(string error) => new(Success: false, Error: error);
}

/// <summary>Facilitates login/logout/register flows on top of <see cref="ILedgerLiteApiClient"/>.</summary>
public interface IAuthService
{
    /// <summary>Validates credentials, persists the returned JWT and publishes the authenticated state.</summary>
    Task<AuthResult> LoginAsync(string email, string password, CancellationToken cancellationToken = default);

    /// <summary>Creates a new user account. Returns an error for validation problems and duplicate emails.</summary>
    Task<AuthResult> RegisterAsync(string email, string displayName, string password, CancellationToken cancellationToken = default);

    /// <summary>Clears the stored token and publishes the anonymous state.</summary>
    Task LogoutAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Default <see cref="IAuthService"/>. Errors are reported as <see cref="AuthResult"/> values
/// (never thrown) so login/register forms can render them directly.
/// </summary>
public sealed class AuthService(
    ILedgerLiteApiClient apiClient,
    ITokenStore tokenStore,
    JwtAuthenticationStateProvider authenticationStateProvider) : IAuthService
{
    public async Task<AuthResult> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await apiClient.LoginAsync(new LoginRequest(Email: email, Password: password), cancellationToken);
            await tokenStore.SetAsync(response.AccessToken, response.ExpiresAtUtc, response.Email, cancellationToken);
            authenticationStateProvider.MarkUserAsAuthenticated(response.AccessToken, response.Email);
            return AuthResult.Ok;
        }
        catch (ApiException ex) when (ex.StatusCode == 401)
        {
            return AuthResult.Fail("Invalid email or password.");
        }
        catch (ApiException ex) when (ex.StatusCode == 429)
        {
            return AuthResult.Fail("Too many login attempts. Please wait a minute and try again.");
        }
        catch (ApiException ex)
        {
            return AuthResult.Fail(ex.PrimaryError);
        }
        catch (HttpRequestException)
        {
            return AuthResult.Fail("Could not reach the LedgerLite API. Make sure it is running.");
        }
    }

    public async Task<AuthResult> RegisterAsync(string email, string displayName, string password, CancellationToken cancellationToken = default)
    {
        try
        {
            await apiClient.RegisterAsync(new RegisterRequest(Email: email, DisplayName: displayName, Password: password), cancellationToken);
            return AuthResult.Ok;
        }
        catch (ApiException ex)
        {
            // Surfaces messages like "A user with this email address already exists."
            // or the first FluentValidation message for invalid input.
            return AuthResult.Fail(ex.PrimaryError);
        }
        catch (HttpRequestException)
        {
            return AuthResult.Fail("Could not reach the LedgerLite API. Make sure it is running.");
        }
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        await tokenStore.ClearAsync(cancellationToken);
        authenticationStateProvider.MarkUserAsLoggedOut();
    }
}
