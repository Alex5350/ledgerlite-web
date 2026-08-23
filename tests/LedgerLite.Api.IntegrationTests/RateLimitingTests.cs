using System.Net;
using System.Net.Http.Json;
using LedgerLite.Api.IntegrationTests.Infrastructure;

namespace LedgerLite.Api.IntegrationTests;

/// <summary>
/// Uses a factory variant that keeps the production 5-per-minute-per-IP login limiter.
/// All WebApplicationFactory traffic shares a single (unknown) remote IP, so the limiter
/// partition is shared across requests to this host, which makes the limit deterministic.
/// A single test consumes the window exactly once.
/// </summary>
public sealed class RateLimitingTests(RateLimitedApiFactory factory) : IClassFixture<RateLimitedApiFactory>
{
    [Fact]
    public async Task Login_AboveFiveAttemptsPerMinute_Returns429WithProblemDetails()
    {
        var client = factory.CreateClient();
        var statuses = new List<HttpStatusCode>();

        for (var attempt = 0; attempt < 6; attempt++)
        {
            var response = await client.PostAsJsonAsync("/api/auth/login", new
            {
                email = "rate-limit-probe@ledgerlite.test",
                password = "WrongPassword1!"
            });
            statuses.Add(response.StatusCode);
        }

        // The first five attempts are legitimate authentication failures (401)...
        Assert.Equal(5, statuses.Count(status => status == HttpStatusCode.Unauthorized));
        // ...and the sixth trips the fixed-window limiter (429 + ProblemDetails body).
        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[5]);

        var rejected = statuses[5] == HttpStatusCode.TooManyRequests
            ? await RateLimitProbe(client)
            : throw new InvalidOperationException("Limiter did not trigger.");
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.Equal("Too many requests.", await ApiTestHelpers.ReadProblemTitleAsync(rejected));
    }

    private static async Task<HttpResponseMessage> RateLimitProbe(HttpClient client) =>
        await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "rate-limit-probe@ledgerlite.test",
            password = "WrongPassword1!"
        });
}
