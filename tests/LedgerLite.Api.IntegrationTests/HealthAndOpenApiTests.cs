using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LedgerLite.Api.IntegrationTests.Infrastructure;

namespace LedgerLite.Api.IntegrationTests;

public sealed class HealthAndOpenApiTests(LedgerLiteApiFactory factory) : IClassFixture<LedgerLiteApiFactory>
{
    [Fact]
    public async Task Health_Returns200WithHealthyStatus()
    {
        var response = await factory.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task OpenApiDocument_Returns200AndDocumentsEveryApiPath()
    {
        var response = await factory.CreateClient().GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = await response.Content.ReadFromJsonAsync<JsonElement>();
        var paths = document.GetProperty("paths").EnumerateObject().Select(p => p.Name).ToHashSet();

        var expectedPaths = new[]
        {
            "/api/auth/register",
            "/api/auth/login",
            "/api/periods",
            "/api/periods/{id}/close",
            "/api/accounts",
            "/api/accounts/{id}",
            "/api/journal-entries",
            "/api/reports/trial-balance",
            "/api/budgets",
            "/api/budgets/evaluate"
        };

        foreach (var expected in expectedPaths)
        {
            Assert.True(paths.Contains(expected), $"OpenAPI document is missing path '{expected}'.");
        }
    }

    [Fact]
    public async Task OpenApiDocument_DeclaresBearerSchemeForProtectedEndpoints()
    {
        var response = await factory.CreateClient().GetAsync("/openapi/v1.json");
        var document = await response.Content.ReadFromJsonAsync<JsonElement>();

        var bearer = document
            .GetProperty("components")
            .GetProperty("securitySchemes")
            .GetProperty("Bearer");
        Assert.Equal("http", bearer.GetProperty("type").GetString());
        Assert.Equal("bearer", bearer.GetProperty("scheme").GetString());
        Assert.Equal("JWT", bearer.GetProperty("bearerFormat").GetString());

        // Protected operations reference the scheme; public auth endpoints do not.
        var createPeriod = document.GetProperty("paths").GetProperty("/api/periods").GetProperty("post");
        Assert.True(createPeriod.TryGetProperty("security", out _), "POST /api/periods must require Bearer auth.");
        var login = document.GetProperty("paths").GetProperty("/api/auth/login").GetProperty("post");
        Assert.False(login.TryGetProperty("security", out _), "POST /api/auth/login must not require auth.");
    }
}
