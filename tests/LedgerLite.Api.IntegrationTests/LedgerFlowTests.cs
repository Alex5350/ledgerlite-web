using System.Net;
using System.Net.Http.Json;
using LedgerLite.Api.IntegrationTests.Infrastructure;

namespace LedgerLite.Api.IntegrationTests;

/// <summary>End-to-end flow: period -> accounts -> entries -> trial balance -> budgets -> evaluation.</summary>
public sealed class LedgerFlowTests(LedgerLiteApiFactory factory) : IClassFixture<LedgerLiteApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<HttpClient> AuthenticatedClientAsync()
    {
        var (token, _, _) = await ApiTestHelpers.RegisterAndLoginAsync(_client);
        return ApiTestHelpers.CreateAuthenticatedClient(factory, token);
    }

    [Fact]
    public async Task FullLedgerFlow_SupportsPostingReportingAndBudgeting()
    {
        using var client = await AuthenticatedClientAsync();

        // Period
        var periodId = await ApiTestHelpers.CreatePeriodAsync(client, name: "September 2026");

        // Accounts
        var cashId = await ApiTestHelpers.CreateAccountAsync(client, periodId, "1010", "Cash", "Asset");
        var equityId = await ApiTestHelpers.CreateAccountAsync(client, periodId, "3010", "Equity", "Equity");

        // Balanced entry
        var posted = await ApiTestHelpers.PostEntryAsync(
            client, periodId, "Owner investment",
            (cashId, 500m, 0m),
            (equityId, 0m, 500m));
        Assert.Equal(HttpStatusCode.Created, posted.StatusCode);

        // The created entry is retrievable through the paged listing.
        var page = await client.GetFromJsonAsync<PagedResponse>(
            $"/api/journal-entries?periodId={periodId}&page=1&pageSize=20");
        var entry = Assert.Single(page!.Items);
        Assert.True(entry.IsPosted);
        Assert.Equal("Owner investment", entry.Description);
        Assert.Equal(2, entry.Lines.Count);

        // Trial balance is balanced.
        var trialBalance = await client.GetFromJsonAsync<TrialBalanceResponse>(
            $"/api/reports/trial-balance?periodId={periodId}");
        Assert.NotNull(trialBalance);
        Assert.True(trialBalance.IsBalanced);
        Assert.Equal(500m, trialBalance.TotalDebits);
        Assert.Equal(500m, trialBalance.TotalCredits);
        Assert.Equal(2, trialBalance.Lines.Count);
        var cashLine = trialBalance.Lines.Single(line => line.AccountId == cashId);
        Assert.Equal(500m, cashLine.Balance);   // asset: debits - credits
        var equityLine = trialBalance.Lines.Single(line => line.AccountId == equityId);
        Assert.Equal(500m, equityLine.Balance); // equity: credits - debits
    }

    [Fact]
    public async Task PostEntry_WhenUnbalanced_Returns400ValidationProblem()
    {
        using var client = await AuthenticatedClientAsync();
        var periodId = await ApiTestHelpers.CreatePeriodAsync(client);
        var cashId = await ApiTestHelpers.CreateAccountAsync(client, periodId, "1010", "Cash", "Asset");
        var equityId = await ApiTestHelpers.CreateAccountAsync(client, periodId, "3010", "Equity", "Equity");

        var response = await ApiTestHelpers.PostEntryAsync(
            client, periodId, "Unbalanced",
            (cashId, 500m, 0m),
            (equityId, 0m, 490m));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = await ApiTestHelpers.ReadValidationErrorsAsync(response);
        Assert.Contains("JournalEntries.Invalid", errors.Keys);
        Assert.Contains(errors["JournalEntries.Invalid"], message => message.Contains("not balanced"));
    }

    [Fact]
    public async Task JournalEntries_PagingMetadataReflectsFilterAndPaging()
    {
        using var client = await AuthenticatedClientAsync();
        var periodId = await ApiTestHelpers.CreatePeriodAsync(client);
        var cashId = await ApiTestHelpers.CreateAccountAsync(client, periodId, "1010", "Cash", "Asset");
        var equityId = await ApiTestHelpers.CreateAccountAsync(client, periodId, "3010", "Equity", "Equity");
        await ApiTestHelpers.PostEntryAsync(client, periodId, "First", (cashId, 10m, 0m), (equityId, 0m, 10m));
        await ApiTestHelpers.PostEntryAsync(client, periodId, "Second", (cashId, 20m, 0m), (equityId, 0m, 20m));

        var firstPage = await client.GetFromJsonAsync<PagedResponse>(
            $"/api/journal-entries?periodId={periodId}&page=1&pageSize=1");
        Assert.Equal(2, firstPage!.TotalCount);
        Assert.Equal(1, firstPage.Page);
        Assert.Equal(1, firstPage.PageSize);
        Assert.Single(firstPage.Items);

        var secondPage = await client.GetFromJsonAsync<PagedResponse>(
            $"/api/journal-entries?periodId={periodId}&page=2&pageSize=1");
        Assert.Equal(2, secondPage!.TotalCount);
        Assert.Single(secondPage.Items);
        Assert.NotEqual(firstPage.Items[0].Id, secondPage.Items[0].Id);
    }

    [Fact]
    public async Task Accounts_ListByPeriodReturnsCreatedAccount()
    {
        using var client = await AuthenticatedClientAsync();
        var periodId = await ApiTestHelpers.CreatePeriodAsync(client);
        var accountId = await ApiTestHelpers.CreateAccountAsync(client, periodId, "4242", "Equipment", "Asset");

        var listed = await client.GetFromJsonAsync<List<AccountResponse>>($"/api/accounts?periodId={periodId}");
        var account = Assert.Single(listed!);
        Assert.Equal(accountId, account.Id);
        Assert.Equal("4242", account.Number);
        Assert.Equal("Equipment", account.Name);
        Assert.Equal("Asset", account.Type);

        var fetched = await client.GetFromJsonAsync<AccountResponse>($"/api/accounts/{accountId}");
        Assert.NotNull(fetched);
        Assert.Equal("Equipment", fetched.Name);
    }

    [Fact]
    public async Task Budgets_SetListAndEvaluateAcrossThresholds()
    {
        using var client = await AuthenticatedClientAsync();
        var periodId = await ApiTestHelpers.CreatePeriodAsync(client, name: "Budget period");
        var cashId = await ApiTestHelpers.CreateAccountAsync(client, periodId, "1010", "Cash", "Asset");
        var marketingId = await ApiTestHelpers.CreateAccountAsync(client, periodId, "5010", "Marketing", "Expense");

        // Set + list.
        var set = await client.PostAsJsonAsync("/api/budgets", new
        {
            periodId,
            category = "Marketing",
            limitAmount = 100m,
            currency = "USD"
        });
        Assert.Equal(HttpStatusCode.Created, set.StatusCode);
        var budgets = await client.GetFromJsonAsync<List<BudgetResponse>>($"/api/budgets?periodId={periodId}");
        var budget = Assert.Single(budgets!);
        Assert.Equal("Marketing", budget.Category);
        Assert.Equal(100m, budget.LimitAmount);

        // Spend 90%: first evaluation raises the 80% alert, re-evaluation is quiet.
        await ApiTestHelpers.PostEntryAsync(client, periodId, "Ad campaign",
            (marketingId, 90m, 0m), (cashId, 0m, 90m));
        var firstEvaluation = await PostEvaluateAsync(client, periodId);
        var evaluation = Assert.Single(firstEvaluation);
        Assert.Equal("Marketing", evaluation.Category);
        Assert.Equal(90m, evaluation.SpentAmount);
        Assert.Equal(["EightyPercent"], evaluation.ThresholdsExceeded);

        var reEvaluation = await PostEvaluateAsync(client, periodId);
        Assert.Empty(Assert.Single(reEvaluation).ThresholdsExceeded);

        // Cross 100%: the second alert fires exactly once.
        await ApiTestHelpers.PostEntryAsync(client, periodId, "More ads",
            (marketingId, 10m, 0m), (cashId, 0m, 10m));
        var finalEvaluation = await PostEvaluateAsync(client, periodId);
        Assert.Equal(["HundredPercent"], Assert.Single(finalEvaluation).ThresholdsExceeded);
    }

    [Fact]
    public async Task Periods_ListIncludesSeededAndNewlyCreatedPeriods()
    {
        using var client = await AuthenticatedClientAsync();
        await ApiTestHelpers.CreatePeriodAsync(client, name: "A brand new period");

        var periods = await client.GetFromJsonAsync<List<PeriodResponse>>("/api/periods");

        Assert.NotNull(periods);
        Assert.Contains(periods, period => period.Name == "A brand new period");
        Assert.Contains(periods, period => period.Name == "January 2026" && period.Status == "Open");
    }

    private static async Task<List<BudgetEvaluationResponse>> PostEvaluateAsync(HttpClient client, Guid periodId)
    {
        var response = await client.PostAsJsonAsync("/api/budgets/evaluate", new { periodId });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<List<BudgetEvaluationResponse>>())!;
    }
}
