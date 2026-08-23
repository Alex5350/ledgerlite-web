using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LedgerLite.Web.Client.Services.Api;

/// <summary>
/// Default <see cref="ILedgerLiteApiClient"/> implementation over a pre-configured
/// <see cref="HttpClient"/> (base address + bearer token handler are wired by
/// <see cref="LedgerLiteClientServices"/>). Works unchanged in Interactive Server
/// and WebAssembly hosts.
/// </summary>
internal sealed class LedgerLiteApiClient(HttpClient httpClient) : ILedgerLiteApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public Task<CreatedResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default) =>
        PostAsync<CreatedResponse>("api/auth/register", request, cancellationToken);

    public Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default) =>
        PostAsync<LoginResponse>("api/auth/login", request, cancellationToken);

    public async Task<IReadOnlyList<FiscalPeriodResponse>> GetFiscalPeriodsAsync(CancellationToken cancellationToken = default) =>
        await GetAsync<IReadOnlyList<FiscalPeriodResponse>>("api/periods", cancellationToken);

    public Task<CreatedResponse> CreateFiscalPeriodAsync(CreateFiscalPeriodRequest request, CancellationToken cancellationToken = default) =>
        PostAsync<CreatedResponse>("api/periods", request, cancellationToken);

    public Task CloseFiscalPeriodAsync(Guid periodId, CancellationToken cancellationToken = default) =>
        PostAsync($"api/periods/{periodId}/close", new object(), cancellationToken);

    public async Task<IReadOnlyList<AccountResponse>> GetAccountsAsync(Guid periodId, CancellationToken cancellationToken = default) =>
        await GetAsync<IReadOnlyList<AccountResponse>>($"api/accounts?periodId={periodId}", cancellationToken);

    public Task<CreatedResponse> CreateAccountAsync(CreateAccountRequest request, CancellationToken cancellationToken = default) =>
        PostAsync<CreatedResponse>("api/accounts", request, cancellationToken);

    public Task<PagedResult<JournalEntryResponse>> GetJournalEntriesAsync(
        Guid? periodId = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = periodId is { } id
            ? $"periodId={id}&page={page}&pageSize={pageSize}"
            : $"page={page}&pageSize={pageSize}";

        return GetAsync<PagedResult<JournalEntryResponse>>($"api/journal-entries?{query}", cancellationToken);
    }

    public Task<CreatedResponse> CreateJournalEntryAsync(CreateJournalEntryRequest request, CancellationToken cancellationToken = default) =>
        PostAsync<CreatedResponse>("api/journal-entries", request, cancellationToken);

    public Task<TrialBalanceResponse> GetTrialBalanceAsync(Guid periodId, CancellationToken cancellationToken = default) =>
        GetAsync<TrialBalanceResponse>($"api/reports/trial-balance?periodId={periodId}", cancellationToken);

    public async Task<IReadOnlyList<BudgetResponse>> GetBudgetsAsync(Guid periodId, CancellationToken cancellationToken = default) =>
        await GetAsync<IReadOnlyList<BudgetResponse>>($"api/budgets?periodId={periodId}", cancellationToken);

    public Task<CreatedResponse> CreateBudgetAsync(CreateBudgetRequest request, CancellationToken cancellationToken = default) =>
        PostAsync<CreatedResponse>("api/budgets", request, cancellationToken);

    public Task<IReadOnlyList<BudgetEvaluationResponse>> EvaluateBudgetsAsync(Guid periodId, CancellationToken cancellationToken = default) =>
        PostAsync<IReadOnlyList<BudgetEvaluationResponse>>("api/budgets/evaluate", new { periodId }, cancellationToken);

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw await ApiException.FromResponseAsync(response, cancellationToken);
        }
    }

    private async Task<TResponse> GetAsync<TResponse>(string requestUri, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(requestUri, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await ReadAsync<TResponse>(response, cancellationToken);
    }

    private async Task<TResponse> PostAsync<TResponse>(string requestUri, object request, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync(requestUri, request, JsonOptions, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await ReadAsync<TResponse>(response, cancellationToken);
    }

    private async Task PostAsync(string requestUri, object request, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync(requestUri, request, JsonOptions, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    private static async Task<TResponse> ReadAsync<TResponse>(HttpResponseMessage response, CancellationToken cancellationToken) =>
        await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions, cancellationToken)
        ?? throw new ApiException((int)response.StatusCode, "The API returned an empty or unreadable response body.");
}
