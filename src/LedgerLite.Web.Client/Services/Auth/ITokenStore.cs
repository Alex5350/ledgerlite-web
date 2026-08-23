namespace LedgerLite.Web.Client.Services.Auth;

/// <summary>The persisted authentication session.</summary>
/// <param name="AccessToken">JWT bearer token sent to the API.</param>
/// <param name="ExpiresAtUtc">UTC moment at which the token stops being valid.</param>
/// <param name="Email">Email of the signed-in user.</param>
public sealed record StoredToken(string AccessToken, DateTime ExpiresAtUtc, string Email);

/// <summary>
/// Stores the current session's JWT so that both the <see cref="BearerTokenHandler"/>
/// and <see cref="JwtAuthenticationStateProvider"/> can reach it. Implementations must
/// be usable from Interactive Server (circuit JS interop) and WebAssembly.
/// </summary>
public interface ITokenStore
{
    /// <summary>
    /// Returns the stored token, or <see langword="null"/> when no token is stored or it
    /// has expired (in which case the stored value is also cleared). Safe during prerender.
    /// </summary>
    Task<StoredToken?> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>Persists the token (memory + browser local storage when JS interop is available).</summary>
    Task SetAsync(string accessToken, DateTime expiresAtUtc, string email, CancellationToken cancellationToken = default);

    /// <summary>Removes any stored token.</summary>
    Task ClearAsync(CancellationToken cancellationToken = default);
}
