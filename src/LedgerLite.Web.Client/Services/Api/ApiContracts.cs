using System.Text.Json.Serialization;

namespace LedgerLite.Web.Client.Services.Api;

// Enums mirror the string values produced by the LedgerLite API
// (System.Text.Json with JsonStringEnumConverter on both sides).

/// <summary>Ledger account classification, serialized as a string (e.g. "Asset").</summary>
public enum AccountType
{
    Asset,
    Liability,
    Equity,
    Revenue,
    Expense
}

/// <summary>Lifecycle state of a fiscal period, serialized as a string ("Open"/"Closed").</summary>
public enum FiscalPeriodStatus
{
    Open,
    Closed
}

/// <summary>Budget notification thresholds, serialized as strings ("EightyPercent"/"HundredPercent").</summary>
public enum BudgetThreshold
{
    None,
    EightyPercent,
    HundredPercent
}

// ----- Auth -----

/// <summary>POST /api/auth/register request body.</summary>
public sealed record RegisterRequest(string Email, string DisplayName, string Password);

/// <summary>POST /api/auth/login request body.</summary>
public sealed record LoginRequest(string Email, string Password);

/// <summary>Successful login (200) response body.</summary>
public sealed record LoginResponse(string AccessToken, DateTime ExpiresAtUtc, Guid UserId, string Email);

// ----- Shared -----

/// <summary>Body of every 201 Created response: just the new resource's id.</summary>
public sealed record CreatedResponse(Guid Id);

// ----- Fiscal periods -----

/// <summary>POST /api/periods request body.</summary>
public sealed record CreateFiscalPeriodRequest(string Name, DateOnly StartDate, DateOnly EndDate);

/// <summary>GET /api/periods item.</summary>
public sealed record FiscalPeriodResponse(
    Guid Id,
    string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    FiscalPeriodStatus Status);

// ----- Accounts -----

/// <summary>POST /api/accounts request body.</summary>
public sealed record CreateAccountRequest(string Number, string Name, AccountType Type, Guid PeriodId);

/// <summary>GET /api/accounts item.</summary>
public sealed record AccountResponse(Guid Id, string Number, string Name, AccountType Type, Guid FiscalPeriodId);

// ----- Journal entries -----

/// <summary>A single debit/credit line; used in both create requests and entry responses.</summary>
public sealed record JournalEntryLine(Guid AccountId, decimal Debit, decimal Credit);

/// <summary>POST /api/journal-entries request body.</summary>
public sealed record CreateJournalEntryRequest(Guid PeriodId, string? Description, IReadOnlyList<JournalEntryLine> Lines);

/// <summary>GET /api/journal-entries item.</summary>
public sealed record JournalEntryResponse(
    Guid Id,
    Guid FiscalPeriodId,
    string? Description,
    DateTime OccurredOnUtc,
    bool IsPosted,
    IReadOnlyList<JournalEntryLine> Lines);

/// <summary>Paged result envelope returned by GET /api/journal-entries.</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);

// ----- Reports -----

/// <summary>Per-account totals row of the trial balance.</summary>
public sealed record TrialBalanceLine(
    Guid AccountId,
    string AccountNumber,
    string AccountName,
    AccountType AccountType,
    decimal TotalDebits,
    decimal TotalCredits,
    decimal Balance);

/// <summary>GET /api/reports/trial-balance response body.</summary>
public sealed record TrialBalanceResponse(
    Guid PeriodId,
    IReadOnlyList<TrialBalanceLine> Lines,
    decimal TotalDebits,
    decimal TotalCredits,
    bool IsBalanced);

// ----- Budgets -----

/// <summary>POST /api/budgets request body. Currency is a 3-letter ISO 4217 code (e.g. "USD").</summary>
public sealed record CreateBudgetRequest(Guid PeriodId, string Category, decimal LimitAmount, string Currency);

/// <summary>GET /api/budgets item.</summary>
public sealed record BudgetResponse(Guid Id, Guid FiscalPeriodId, string Category, decimal LimitAmount, string Currency);

/// <summary>POST /api/budgets/evaluate item.</summary>
public sealed record BudgetEvaluationResponse(
    Guid BudgetId,
    string Category,
    decimal LimitAmount,
    string Currency,
    decimal SpentAmount,
    IReadOnlyList<BudgetThreshold> ThresholdsExceeded);
