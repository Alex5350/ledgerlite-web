using System.Net;
using System.Net.Http.Json;
using LedgerLite.Api.IntegrationTests.Infrastructure;

namespace LedgerLite.Api.IntegrationTests;

public sealed class AuthTests(LedgerLiteApiFactory factory) : IClassFixture<LedgerLiteApiFactory>
{
    [Fact]
    public async Task Register_WithValidUser_Returns201WithLocation()
    {
        var email = ApiTestHelpers.UniqueEmail();

        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/register", new
        {
            email,
            displayName = "New User",
            password = ApiTestHelpers.TestPassword
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.StartsWith("/api/users/", response.Headers.Location?.ToString(), StringComparison.Ordinal);
        var body = await response.Content.ReadFromJsonAsync<IdResponse>();
        Assert.NotEqual(Guid.Empty, body!.Id);
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_Returns409()
    {
        var email = ApiTestHelpers.UniqueEmail();
        var client = factory.CreateClient();
        var first = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            displayName = "New User",
            password = ApiTestHelpers.TestPassword
        });
        first.EnsureSuccessStatusCode();

        var second = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            displayName = "Someone Else",
            password = "AnotherPass123!"
        });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("Users.EmailAlreadyInUse", await ApiTestHelpers.ReadProblemTitleAsync(second));
    }

    [Fact]
    public async Task Register_WithInvalidEmail_Returns400ValidationProblem()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/register", new
        {
            email = "not-an-email",
            displayName = "New User",
            password = ApiTestHelpers.TestPassword
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = await ApiTestHelpers.ReadValidationErrorsAsync(response);
        Assert.Contains("Users.InvalidEmail", errors.Keys);
    }

    [Fact]
    public async Task Register_WithShortPassword_Returns400ValidationProblem()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/register", new
        {
            email = ApiTestHelpers.UniqueEmail(),
            displayName = "New User",
            password = "short"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = await ApiTestHelpers.ReadValidationErrorsAsync(response);
        Assert.Contains("Password", errors.Keys);
    }

    [Fact]
    public async Task Login_WithCorrectCredentials_Returns200WithUsableToken()
    {
        var client = factory.CreateClient();
        var (email, userId) = await RegisterUserAsync(client);

        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = ApiTestHelpers.TestPassword });

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var session = await login.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.False(string.IsNullOrWhiteSpace(session!.AccessToken));
        Assert.Equal(userId, session.UserId);
        Assert.Equal(email, session.Email);
        Assert.True(session.ExpiresAtUtc > DateTime.UtcNow);

        // The token actually works on a protected endpoint.
        using var authorized = ApiTestHelpers.CreateAuthenticatedClient(factory, session.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await authorized.GetAsync("/api/periods")).StatusCode);
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401ProblemDetails()
    {
        var client = factory.CreateClient();
        var email = ApiTestHelpers.UniqueEmail();
        await RegisterUserAsync(client, email);

        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "WrongPassword1!" });

        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        Assert.Equal("Auth.InvalidCredentials", await ApiTestHelpers.ReadProblemTitleAsync(login));
    }

    [Fact]
    public async Task Login_WithUnknownEmail_Returns401()
    {
        var login = await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new
        {
            email = "ghost@ledgerlite.test",
            password = "Whatever123!"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task Login_WithMalformedEmail_Returns401()
    {
        var login = await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new
        {
            email = "not-an-email",
            password = "Whatever123!"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task Login_WithSeededDemoUser_Returns200()
    {
        // The Development seed created demo@ledgerlite.io / Demo123!.
        var login = await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new
        {
            email = "demo@ledgerlite.io",
            password = "Demo123!"
        });

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var session = await login.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.Equal("demo@ledgerlite.io", session!.Email);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_Returns401()
    {
        var response = await factory.CreateClient().GetAsync("/api/periods");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithInvalidToken_Returns401()
    {
        using var client = ApiTestHelpers.CreateAuthenticatedClient(factory, "this-is-not-a-jwt");

        var response = await client.GetAsync("/api/accounts/" + Guid.NewGuid());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task<(string Email, Guid UserId)> RegisterUserAsync(HttpClient client, string? email = null)
    {
        email ??= ApiTestHelpers.UniqueEmail();
        var registered = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            displayName = "Auth Tester",
            password = ApiTestHelpers.TestPassword
        });
        registered.EnsureSuccessStatusCode();
        var body = await registered.Content.ReadFromJsonAsync<IdResponse>();
        return (email, body!.Id);
    }
}
