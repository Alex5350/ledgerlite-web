using System.Net;
using System.Net.Http.Json;
using LedgerLite.Api.IntegrationTests.Infrastructure;

namespace LedgerLite.Api.IntegrationTests;

public sealed class ValidationAndConflictTests(LedgerLiteApiFactory factory) : IClassFixture<LedgerLiteApiFactory>
{
    private async Task<HttpClient> AuthenticatedClientAsync()
    {
        var client = factory.CreateClient();
        var (token, _, _) = await ApiTestHelpers.RegisterAndLoginAsync(client);
        return ApiTestHelpers.CreateAuthenticatedClient(factory, token);
    }

    [Fact]
    public async Task CreatePeriod_WithEndDateBeforeStartDate_Returns400()
    {
        using var client = await AuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/periods", new
        {
            name = "Backwards",
            startDate = new DateOnly(2026, 3, 31),
            endDate = new DateOnly(2026, 1, 1)
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = await ApiTestHelpers.ReadValidationErrorsAsync(response);
        Assert.Contains("EndDate", errors.Keys);
    }

    [Fact]
    public async Task CreateAccount_WithInvalidNumber_Returns400()
    {
        using var client = await AuthenticatedClientAsync();
        var periodId = await ApiTestHelpers.CreatePeriodAsync(client);

        var response = await client.PostAsJsonAsync("/api/accounts", new
        {
            number = "0999",
            name = "Below range",
            type = "Asset",
            periodId
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = await ApiTestHelpers.ReadValidationErrorsAsync(response);
        Assert.Contains("Accounts.InvalidNumber", errors.Keys);
    }

    [Fact]
    public async Task CreateAccount_WithDuplicateNumberInPeriod_Returns409()
    {
        using var client = await AuthenticatedClientAsync();
        var periodId = await ApiTestHelpers.CreatePeriodAsync(client);
        await ApiTestHelpers.CreateAccountAsync(client, periodId, "1010", "Cash", "Asset");

        var response = await client.PostAsJsonAsync("/api/accounts", new
        {
            number = "1010",
            name = "Cash duplicate",
            type = "Asset",
            periodId
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("Accounts.NumberTaken", await ApiTestHelpers.ReadProblemTitleAsync(response));
    }

    [Fact]
    public async Task CreateAccount_WithUnknownPeriod_Returns404()
    {
        using var client = await AuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/accounts", new
        {
            number = "1234",
            name = "Orphan",
            type = "Asset",
            periodId = Guid.CreateVersion7()
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("FiscalPeriods.NotFound", await ApiTestHelpers.ReadProblemTitleAsync(response));
    }

    [Fact]
    public async Task PostEntry_WithSingleLine_Returns400()
    {
        using var client = await AuthenticatedClientAsync();
        var periodId = await ApiTestHelpers.CreatePeriodAsync(client);
        var accountId = await ApiTestHelpers.CreateAccountAsync(client, periodId);

        var response = await ApiTestHelpers.PostEntryAsync(client, periodId, "Only one line", (accountId, 10m, 0m));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = await ApiTestHelpers.ReadValidationErrorsAsync(response);
        Assert.Contains(errors.Keys, key => key.StartsWith("Lines", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PostEntry_WithUnknownPeriod_Returns404()
    {
        using var client = await AuthenticatedClientAsync();

        var response = await ApiTestHelpers.PostEntryAsync(
            client, Guid.CreateVersion7(), "Unknown period",
            (Guid.CreateVersion7(), 10m, 0m), (Guid.CreateVersion7(), 0m, 10m));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SetBudget_WithZeroLimit_Returns400()
    {
        using var client = await AuthenticatedClientAsync();
        var periodId = await ApiTestHelpers.CreatePeriodAsync(client);

        var response = await client.PostAsJsonAsync("/api/budgets", new
        {
            periodId,
            category = "Groceries",
            limitAmount = 0m,
            currency = "USD"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = await ApiTestHelpers.ReadValidationErrorsAsync(response);
        Assert.Contains("LimitAmount", errors.Keys);
    }

    [Fact]
    public async Task SetBudget_WithDuplicateCategoryInPeriod_Returns409()
    {
        using var client = await AuthenticatedClientAsync();
        var periodId = await ApiTestHelpers.CreatePeriodAsync(client);
        var first = await client.PostAsJsonAsync("/api/budgets", new
        {
            periodId,
            category = "Groceries",
            limitAmount = 200m,
            currency = "USD"
        });
        first.EnsureSuccessStatusCode();

        var second = await client.PostAsJsonAsync("/api/budgets", new
        {
            periodId,
            category = "Groceries",
            limitAmount = 500m,
            currency = "USD"
        });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("Budgets.AlreadyExistsForCategory", await ApiTestHelpers.ReadProblemTitleAsync(second));
    }

    [Fact]
    public async Task SetBudget_WithBadCurrency_Returns400()
    {
        using var client = await AuthenticatedClientAsync();
        var periodId = await ApiTestHelpers.CreatePeriodAsync(client);

        var response = await client.PostAsJsonAsync("/api/budgets", new
        {
            periodId,
            category = "Groceries",
            limitAmount = 100m,
            currency = "DOLLAR"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = await ApiTestHelpers.ReadValidationErrorsAsync(response);
        Assert.Contains("Currency", errors.Keys);
    }

    [Fact]
    public async Task SetBudget_WithSubCentPrecision_Returns400()
    {
        using var client = await AuthenticatedClientAsync();
        var periodId = await ApiTestHelpers.CreatePeriodAsync(client);

        var response = await client.PostAsJsonAsync("/api/budgets", new
        {
            periodId,
            category = "Groceries",
            limitAmount = 10.567m,
            currency = "USD"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = await ApiTestHelpers.ReadValidationErrorsAsync(response);
        Assert.Contains("Budgets.InvalidMoney", errors.Keys);
    }
}
