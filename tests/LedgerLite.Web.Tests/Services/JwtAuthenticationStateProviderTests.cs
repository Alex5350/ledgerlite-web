using System.Security.Claims;
using System.Text;
using LedgerLite.Web.Client.Services.Auth;
using Microsoft.AspNetCore.Components.Authorization;

namespace LedgerLite.Web.Tests.Services;

public sealed class JwtAuthenticationStateProviderTests
{
    private readonly ITokenStore _tokenStore = Substitute.For<ITokenStore>();

    private JwtAuthenticationStateProvider CreateProviderWithToken(StoredToken? storedToken)
    {
        _tokenStore
            .GetAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(storedToken));
        return new JwtAuthenticationStateProvider(_tokenStore);
    }

    private static string Base64Url(string json) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(json))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    /// <summary>Hand-crafts a JWT: base64url(header).base64url(payload).signature.</summary>
    private static string MakeToken(string payloadJson) =>
        $"{Base64Url("{\"alg\":\"HS256\",\"typ\":\"JWT\"}")}.{Base64Url(payloadJson)}.not-a-real-signature";

    private static long UnixSeconds(DateTime utc) => new DateTimeOffset(utc).ToUnixTimeSeconds();

    [Fact]
    public async Task Valid_token_yields_authenticated_principal_with_claims()
    {
        var token = MakeToken(
            $"{{\"sub\":\"user-123\",\"email\":\"alex@ledgerlite.io\",\"exp\":{UnixSeconds(DateTime.UtcNow.AddHours(1))}}}");
        var provider = CreateProviderWithToken(new StoredToken(token, DateTime.UtcNow.AddHours(1), "alex@ledgerlite.io"));

        var state = await provider.GetAuthenticationStateAsync();

        Assert.True(state.User.Identity?.IsAuthenticated);
        Assert.Equal("Bearer", state.User.Identity!.AuthenticationType);
        Assert.Equal("user-123", state.User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        Assert.Equal("alex@ledgerlite.io", state.User.FindFirst(ClaimTypes.Name)?.Value);
        Assert.Equal("alex@ledgerlite.io", state.User.FindFirst(ClaimTypes.Email)?.Value);
    }

    [Fact]
    public async Task Missing_email_claim_falls_back_to_stored_email()
    {
        var token = MakeToken(
            $"{{\"sub\":\"user-123\",\"exp\":{UnixSeconds(DateTime.UtcNow.AddHours(1))}}}");
        var provider = CreateProviderWithToken(new StoredToken(token, DateTime.UtcNow.AddHours(1), "fallback@ledgerlite.io"));

        var state = await provider.GetAuthenticationStateAsync();

        Assert.True(state.User.Identity?.IsAuthenticated);
        Assert.Equal("fallback@ledgerlite.io", state.User.FindFirst(ClaimTypes.Email)?.Value);
        Assert.Equal("user-123", state.User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
    }

    [Fact]
    public async Task Expired_token_yields_anonymous()
    {
        var token = MakeToken($"{{\"sub\":\"user-123\",\"exp\":{UnixSeconds(DateTime.UtcNow.AddHours(-1))}}}");
        var provider = CreateProviderWithToken(new StoredToken(token, DateTime.UtcNow.AddHours(-1), "alex@ledgerlite.io"));

        var state = await provider.GetAuthenticationStateAsync();

        Assert.False(state.User.Identity?.IsAuthenticated);
    }

    [Fact]
    public async Task Garbage_token_yields_anonymous()
    {
        var provider = CreateProviderWithToken(new StoredToken("not-a-jwt", DateTime.UtcNow.AddHours(1), "alex@ledgerlite.io"));

        var state = await provider.GetAuthenticationStateAsync();

        Assert.False(state.User.Identity?.IsAuthenticated);
    }

    [Fact]
    public async Task Malformed_two_segment_token_yields_anonymous()
    {
        var provider = CreateProviderWithToken(new StoredToken("only.two", DateTime.UtcNow.AddHours(1), "alex@ledgerlite.io"));

        var state = await provider.GetAuthenticationStateAsync();

        Assert.False(state.User.Identity?.IsAuthenticated);
    }

    [Fact]
    public async Task Non_json_payload_yields_anonymous()
    {
        var provider = CreateProviderWithToken(new StoredToken(
            $"{Base64Url("h")}.{Base64Url("%%%not json%%%")}.sig",
            DateTime.UtcNow.AddHours(1),
            "alex@ledgerlite.io"));

        var state = await provider.GetAuthenticationStateAsync();

        Assert.False(state.User.Identity?.IsAuthenticated);
    }

    [Fact]
    public async Task Missing_token_yields_anonymous()
    {
        var provider = CreateProviderWithToken(null);

        var state = await provider.GetAuthenticationStateAsync();

        Assert.False(state.User.Identity?.IsAuthenticated);
    }

    [Fact]
    public async Task MarkUserAsLoggedOut_publishes_anonymous_state_and_event()
    {
        var token = MakeToken(
            $"{{\"sub\":\"user-123\",\"email\":\"alex@ledgerlite.io\",\"exp\":{UnixSeconds(DateTime.UtcNow.AddHours(1))}}}");
        var provider = CreateProviderWithToken(new StoredToken(token, DateTime.UtcNow.AddHours(1), "alex@ledgerlite.io"));

        var before = await provider.GetAuthenticationStateAsync();
        Assert.True(before.User.Identity?.IsAuthenticated);

        AuthenticationState? published = null;
        provider.AuthenticationStateChanged += state => published = state.Result;

        // Logout clears the store first (as AuthService.LogoutAsync does), then publishes.
        _tokenStore
            .GetAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<StoredToken?>(null));
        provider.MarkUserAsLoggedOut();

        Assert.NotNull(published);
        Assert.False(published!.User.Identity?.IsAuthenticated);

        var after = await provider.GetAuthenticationStateAsync();
        Assert.False(after.User.Identity?.IsAuthenticated);
    }
}
