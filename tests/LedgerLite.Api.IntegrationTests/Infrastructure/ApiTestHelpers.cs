using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace LedgerLite.Api.IntegrationTests.Infrastructure;

/// <summary>Typed response bodies used by the integration tests.</summary>
internal sealed record IdResponse(Guid Id);

internal sealed record LoginResponse(string AccessToken, DateTime ExpiresAtUtc, Guid UserId, string Email);

internal sealed record AccountResponse(Guid Id, string Number, string Name, string Type, Guid FiscalPeriodId);

internal sealed record PeriodResponse(Guid Id, string Name, DateOnly StartDate, DateOnly EndDate, string Status);

internal sealed record TrialBalanceLine(
    Guid AccountId,
    string AccountNumber,
    string AccountName,
    string AccountType,
    decimal TotalDebits,
    decimal TotalCredits,
    decimal Balance);

internal sealed record TrialBalanceResponse(
    Guid PeriodId,
    List<TrialBalanceLine> Lines,
    decimal TotalDebits,
    decimal TotalCredits,
    bool IsBalanced);

internal sealed record JournalEntryLineResponse(Guid AccountId, decimal Debit, decimal Credit);

internal sealed record JournalEntryResponse(
    Guid Id,
    Guid FiscalPeriodId,
    string? Description,
    DateTime OccurredOnUtc,
    bool IsPosted,
    List<JournalEntryLineResponse> Lines);

internal sealed record PagedResponse(List<JournalEntryResponse> Items, int TotalCount, int Page, int PageSize);

internal sealed record BudgetResponse(Guid Id, Guid FiscalPeriodId, string Category, decimal LimitAmount, string Currency);

internal sealed record BudgetEvaluationResponse(
    Guid BudgetId,
    string Category,
    decimal LimitAmount,
    string Currency,
    decimal SpentAmount,
    List<string> ThresholdsExceeded);

/// <summary>Helpers for the common register -> login -> act flow. Each test uses a unique user.</summary>
internal static class ApiTestHelpers
{
    public const string TestPassword = "Password123!";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static string UniqueEmail(string prefix = "user") =>
        $"{prefix}-{Guid.NewGuid():N}@ledgerlite.test";

    public static async Task<(string Token, Guid UserId, string Email)> RegisterAndLoginAsync(
        HttpClient client,
        string? email = null)
    {
        email ??= UniqueEmail();
        var registered = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            displayName = "Integration Tester",
            password = TestPassword
        });
        registered.EnsureSuccessStatusCode();

        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = TestPassword });
        login.EnsureSuccessStatusCode();
        var session = await login.Content.ReadFromJsonAsync<LoginResponse>(Json)
            ?? throw new InvalidOperationException("Login returned no body.");

        return (session.AccessToken, session.UserId, session.Email);
    }

    public static HttpClient CreateAuthenticatedClient(LedgerLiteApiFactory factory, string token)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public static async Task<Guid> CreatePeriodAsync(
        HttpClient client,
        string? name = null,
        DateOnly? start = null,
        DateOnly? end = null)
    {
        var response = await client.PostAsJsonAsync("/api/periods", new
        {
            name = name ?? $"Test period {Guid.NewGuid():N}",
            startDate = start ?? new DateOnly(2026, 9, 1),
            endDate = end ?? new DateOnly(2026, 9, 30)
        });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<IdResponse>(Json);
        return body!.Id;
    }

    public static async Task<Guid> CreateAccountAsync(
        HttpClient client,
        Guid periodId,
        string number = "1010",
        string name = "Cash",
        string type = "Asset")
    {
        var response = await client.PostAsJsonAsync("/api/accounts", new
        {
            number,
            name,
            type,
            periodId
        });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<IdResponse>(Json);
        return body!.Id;
    }

    public static Task<HttpResponseMessage> PostEntryAsync(
        HttpClient client,
        Guid periodId,
        string? description,
        params (Guid AccountId, decimal Debit, decimal Credit)[] lines) =>
        client.PostAsJsonAsync("/api/journal-entries", new
        {
            periodId,
            description,
            lines = lines.Select(line => new { accountId = line.AccountId, debit = line.Debit, credit = line.Credit })
        });

    /// <summary>Reads a ProblemDetails-shaped body and returns its "errors" keys (validation codes).</summary>
    public static async Task<Dictionary<string, List<string>>> ReadValidationErrorsAsync(HttpResponseMessage response)
    {
        var document = await response.Content.ReadFromJsonAsync<JsonElement>();
        var errors = new Dictionary<string, List<string>>();
        if (document.ValueKind == JsonValueKind.Object && document.TryGetProperty("errors", out var errorsElement))
        {
            foreach (var property in errorsElement.EnumerateObject())
            {
                errors[property.Name] = [.. property.Value.EnumerateArray().Select(e => e.GetString() ?? string.Empty)];
            }
        }

        return errors;
    }

    public static async Task<string> ReadProblemTitleAsync(HttpResponseMessage response)
    {
        var document = await response.Content.ReadFromJsonAsync<JsonElement>();
        return document.ValueKind == JsonValueKind.Object && document.TryGetProperty("title", out var title)
            ? title.GetString()!
            : string.Empty;
    }
}
