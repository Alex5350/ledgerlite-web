using System.Net;
using System.Net.Http.Json;
using LedgerLite.Api.IntegrationTests.Infrastructure;

namespace LedgerLite.Api.IntegrationTests;

public sealed class NotFoundTests(LedgerLiteApiFactory factory) : IClassFixture<LedgerLiteApiFactory>
{
    private async Task<HttpClient> AuthenticatedClientAsync()
    {
        var client = factory.CreateClient();
        var (token, _, _) = await ApiTestHelpers.RegisterAndLoginAsync(client);
        return ApiTestHelpers.CreateAuthenticatedClient(factory, token);
    }

    [Fact]
    public async Task GetAccount_WithUnknownId_Returns404()
    {
        using var client = await AuthenticatedClientAsync();

        var response = await client.GetAsync("/api/accounts/" + Guid.CreateVersion7());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Accounts.NotFound", await ApiTestHelpers.ReadProblemTitleAsync(response));
    }

    [Fact]
    public async Task ClosePeriod_WithUnknownId_Returns404()
    {
        using var client = await AuthenticatedClientAsync();

        var response = await client.PostAsync($"/api/periods/{Guid.CreateVersion7()}/close", content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("FiscalPeriods.NotFound", await ApiTestHelpers.ReadProblemTitleAsync(response));
    }

    [Fact]
    public async Task TrialBalance_ForPeriodWithoutAccounts_Returns404()
    {
        using var client = await AuthenticatedClientAsync();

        var response = await client.GetAsync($"/api/reports/trial-balance?periodId={Guid.CreateVersion7()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task EvaluateBudgets_ForPeriodWithoutBudgets_Returns404()
    {
        using var client = await AuthenticatedClientAsync();
        var periodId = await ApiTestHelpers.CreatePeriodAsync(client);

        var response = await client.PostAsJsonAsync("/api/budgets/evaluate", new { periodId });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Budgets.NotFound", await ApiTestHelpers.ReadProblemTitleAsync(response));
    }

    [Fact]
    public async Task PostEntry_WithUnknownAccount_Returns404()
    {
        using var client = await AuthenticatedClientAsync();
        var periodId = await ApiTestHelpers.CreatePeriodAsync(client);
        var cashId = await ApiTestHelpers.CreateAccountAsync(client, periodId, "1010", "Cash", "Asset");
        var ghostAccountId = Guid.CreateVersion7();

        var response = await ApiTestHelpers.PostEntryAsync(
            client, periodId, "Ghost account", (cashId, 10m, 0m), (ghostAccountId, 0m, 10m));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("JournalEntries.AccountNotFound", await ApiTestHelpers.ReadProblemTitleAsync(response));
    }

    [Fact]
    public async Task ListBudgets_ForUnknownPeriod_Returns200WithEmptyArray()
    {
        using var client = await AuthenticatedClientAsync();

        var budgets = await client.GetFromJsonAsync<List<BudgetResponse>>($"/api/budgets?periodId={Guid.NewGuid()}");

        Assert.Empty(budgets!);
    }
}
