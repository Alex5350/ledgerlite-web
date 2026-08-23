using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;

namespace LedgerLite.Web.Client.Services.Auth;

/// <summary>
/// <see cref="AuthenticationStateProvider"/> that derives the current user from the JWT
/// stored in the <see cref="ITokenStore"/>. The token payload (middle base64url segment)
/// is decoded with System.Text.Json — no external JWT package. Returns an anonymous
/// principal when no token, an invalid token, or an expired token is stored.
/// </summary>
public sealed class JwtAuthenticationStateProvider(ITokenStore tokenStore) : AuthenticationStateProvider
{
    private const string AuthenticationType = "Bearer";

    private static readonly AuthenticationState AnonymousState = new(new ClaimsPrincipal(new ClaimsIdentity()));

    private AuthenticationState? _cachedState;

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var token = await tokenStore.GetAsync();
        if (token is null)
        {
            _cachedState = null;
            return AnonymousState;
        }

        if (_cachedState is not null)
        {
            return _cachedState;
        }

        var principal = ParsePrincipal(token.AccessToken, token.Email);
        if (principal is null)
        {
            _cachedState = null;
            return AnonymousState;
        }

        _cachedState = new AuthenticationState(principal);
        return _cachedState;
    }

    /// <summary>Call after a successful login to publish the new authentication state.</summary>
    public void MarkUserAsAuthenticated(string accessToken, string email)
    {
        var principal = ParsePrincipal(accessToken, email);
        if (principal is null)
        {
            return; // Malformed token: keep the current (anonymous) state.
        }

        _cachedState = new AuthenticationState(principal);
        NotifyAuthenticationStateChanged(Task.FromResult(_cachedState));
    }

    /// <summary>Call after logout to publish the anonymous authentication state.</summary>
    public void MarkUserAsLoggedOut()
    {
        _cachedState = null;
        NotifyAuthenticationStateChanged(Task.FromResult(AnonymousState));
    }

    /// <summary>
    /// Decodes the JWT payload into a principal with name identifier / name / email claims.
    /// Returns <see langword="null"/> for structurally invalid or expired tokens.
    /// </summary>
    private static ClaimsPrincipal? ParsePrincipal(string accessToken, string fallbackEmail)
    {
        try
        {
            var payload = DecodePayload(accessToken);
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            // Trust the token's own "exp" claim in addition to the stored expiry.
            if (root.TryGetProperty("exp", out var expElement)
                && expElement.TryGetInt64(out var unixSeconds)
                && DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime <= DateTime.UtcNow)
            {
                return null;
            }

            var subject = GetStringClaim(root, "sub") ?? GetStringClaim(root, "nameid");
            var email = GetStringClaim(root, "email") ?? fallbackEmail;

            var claims = new List<Claim>();
            if (subject is not null)
            {
                claims.Add(new Claim(ClaimTypes.NameIdentifier, subject));
            }

            if (email is not null)
            {
                claims.Add(new Claim(ClaimTypes.Name, email));
                claims.Add(new Claim(ClaimTypes.Email, email));
            }

            return new ClaimsPrincipal(new ClaimsIdentity(claims, AuthenticationType));
        }
        catch (FormatException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? GetStringClaim(JsonElement root, string claimType) =>
        root.TryGetProperty(claimType, out var element)
        && element.ValueKind == JsonValueKind.String
        && element.GetString() is { Length: > 0 } value
            ? value
            : null;

    private static byte[] DecodePayload(string accessToken)
    {
        var segments = accessToken.Split('.');
        if (segments.Length != 3)
        {
            throw new FormatException("Expected a JWS compact serialization with three segments.");
        }

        return Base64UrlDecode(segments[1]);
    }

    private static byte[] Base64UrlDecode(string input)
    {
        var base64 = input.Replace('-', '+').Replace('_', '/');
        return (base64.Length % 4) switch
        {
            0 => Convert.FromBase64String(base64),
            2 => Convert.FromBase64String(base64 + "=="),
            3 => Convert.FromBase64String(base64 + "="),
            _ => throw new FormatException("Invalid base64url segment length.")
        };
    }
}
