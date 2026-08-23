using System.Net;
using System.Net.Http.Json;
using LedgerLite.Api.IntegrationTests.Infrastructure;

namespace LedgerLite.Api.IntegrationTests;

public sealed class PeriodLifecycleTests(LedgerLiteApiFactory factory) : IClassFixture<LedgerLiteApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<(HttpClient Client, Guid PeriodId, Guid CashId, Guid EquityId)> CreateClosedPeriodScenarioAsync()
    {
        var (token, _, _) = await ApiTestHelpers.RegisterAndLoginAsync(_client);
        var client = ApiTestHelpers.CreateAuthenticatedClient(factory, token);
        // A period whose end date has already passed, so it can be closed today.
        var periodId = await ApiTestHelpers.CreatePeriodAsync(
            client, name: "January 2026 (test)", start: new DateOnly(2026, 1, 1), end: new DateOnly(2026, 1, 31));
        var cashId = await ApiTestHelpers.CreateAccountAsync(client, periodId, "1010", "Cash", "Asset");
        var equityId = await ApiTestHelpers.CreateAccountAsync(client, periodId, "3010", "Equity", "Equity");
        return (client, periodId, cashId, equityId);
    }

    [Fact]
    public async Task Close_AfterEndDate_Returns204AndBlocksFurtherPosting()
    {
        var (client, periodId, cashId, equityId) = await CreateClosedPeriodScenarioAsync();
        var posting = await ApiTestHelpers.PostEntryAsync(
            client, periodId, "Before close", (cashId, 100m, 0m), (equityId, 0m, 100m));
        Assert.Equal(HttpStatusCode.Created, posting.StatusCode);

        var close = await client.PostAsync($"/api/periods/{periodId}/close", content: null);

        Assert.Equal(HttpStatusCode.NoContent, close.StatusCode);

        var afterClose = await ApiTestHelpers.PostEntryAsync(
            client, periodId, "After close", (cashId, 1m, 0m), (equityId, 0m, 1m));
        Assert.Equal(HttpStatusCode.Conflict, afterClose.StatusCode);
        Assert.Equal("FiscalPeriods.ClosedForPosting", await ApiTestHelpers.ReadProblemTitleAsync(afterClose));

        // Reports remain readable after closing.
        var trialBalance = await client.GetFromJsonAsync<TrialBalanceResponse>(
            $"/api/reports/trial-balance?periodId={periodId}");
        Assert.True(trialBalance!.IsBalanced);
    }

    [Fact]
    public async Task Close_SecondTime_Returns409()
    {
        var (client, periodId, _, _) = await CreateClosedPeriodScenarioAsync();
        var first = await client.PostAsync($"/api/periods/{periodId}/close", content: null);
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);

        var second = await client.PostAsync($"/api/periods/{periodId}/close", content: null);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("FiscalPeriods.CannotClose", await ApiTestHelpers.ReadProblemTitleAsync(second));
    }

    [Fact]
    public async Task Close_WithFuturePeriod_Returns409()
    {
        var (token, _, _) = await ApiTestHelpers.RegisterAndLoginAsync(_client);
        using var client = ApiTestHelpers.CreateAuthenticatedClient(factory, token);
        var periodId = await ApiTestHelpers.CreatePeriodAsync(
            client, name: "Far future period",
            start: DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(6)),
            end: DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(7)));

        var close = await client.PostAsync($"/api/periods/{periodId}/close", content: null);

        Assert.Equal(HttpStatusCode.Conflict, close.StatusCode);
        Assert.Equal("FiscalPeriods.CannotClose", await ApiTestHelpers.ReadProblemTitleAsync(close));
    }
}
