namespace LedgerLite.Web.Client.Services.Api;

/// <summary>
/// Typed client for the LedgerLite REST API (http://localhost:5080 by default).
/// All methods throw <see cref="ApiException"/> on non-success responses; network
/// failures propagate as <see cref="HttpRequestException"/>.
/// </summary>
public interface ILedgerLiteApiClient
{
    /// <summary>POST /api/auth/register — creates a user account.</summary>
    Task<CreatedResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);

    /// <summary>POST /api/auth/login — exchanges credentials for a JWT. Rate-limited (5/min/IP).</summary>
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

    /// <summary>GET /api/periods — all fiscal periods.</summary>
    Task<IReadOnlyList<FiscalPeriodResponse>> GetFiscalPeriodsAsync(CancellationToken cancellationToken = default);

    /// <summary>POST /api/periods — creates a fiscal period.</summary>
    Task<CreatedResponse> CreateFiscalPeriodAsync(CreateFiscalPeriodRequest request, CancellationToken cancellationToken = default);

    /// <summary>POST /api/periods/{id}/close — closes a period (204). 409 if already closed / end date not reached.</summary>
    Task CloseFiscalPeriodAsync(Guid periodId, CancellationToken cancellationToken = default);

    /// <summary>GET /api/accounts?periodId= — accounts of a period.</summary>
    Task<IReadOnlyList<AccountResponse>> GetAccountsAsync(Guid periodId, CancellationToken cancellationToken = default);

    /// <summary>POST /api/accounts — creates an account in a period.</summary>
    Task<CreatedResponse> CreateAccountAsync(CreateAccountRequest request, CancellationToken cancellationToken = default);

    /// <summary>GET /api/journal-entries?periodId=&amp;page=&amp;pageSize= — paged entries, newest first.</summary>
    Task<PagedResult<JournalEntryResponse>> GetJournalEntriesAsync(
        Guid? periodId = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    /// <summary>POST /api/journal-entries — posts a balanced entry (400 for domain rule violations, 409 for conflicts).</summary>
    Task<CreatedResponse> CreateJournalEntryAsync(CreateJournalEntryRequest request, CancellationToken cancellationToken = default);

    /// <summary>GET /api/reports/trial-balance?periodId= — per-account debits/credits of posted entries.</summary>
    Task<TrialBalanceResponse> GetTrialBalanceAsync(Guid periodId, CancellationToken cancellationToken = default);

    /// <summary>GET /api/budgets?periodId= — budgets of a period.</summary>
    Task<IReadOnlyList<BudgetResponse>> GetBudgetsAsync(Guid periodId, CancellationToken cancellationToken = default);

    /// <summary>POST /api/budgets — sets a budget for a category. Currency is required (e.g. "USD").</summary>
    Task<CreatedResponse> CreateBudgetAsync(CreateBudgetRequest request, CancellationToken cancellationToken = default);

    /// <summary>POST /api/budgets/evaluate — re-evaluates budgets against posted spending and returns the evaluations.</summary>
    Task<IReadOnlyList<BudgetEvaluationResponse>> EvaluateBudgetsAsync(Guid periodId, CancellationToken cancellationToken = default);
}
