using ErrorOr;
using LedgerLite.Application.Abstractions;
using LedgerLite.Application.Common;
using LedgerLite.Domain.Accounts;

namespace LedgerLite.Application.Features.JournalEntries;

public sealed record GetTrialBalanceQuery(Guid PeriodId);

public sealed record TrialBalanceLineDto(
    Guid AccountId,
    string AccountNumber,
    string AccountName,
    string AccountType,
    decimal TotalDebits,
    decimal TotalCredits,
    decimal Balance);

/// <summary>Per-account debits/credits for a period. Totals must net to zero (double-entry rule).</summary>
public sealed record TrialBalanceDto(
    Guid PeriodId,
    IReadOnlyList<TrialBalanceLineDto> Lines,
    decimal TotalDebits,
    decimal TotalCredits)
{
    public bool IsBalanced => TotalDebits == TotalCredits;
}

public sealed class GetTrialBalanceHandler(
    IJournalEntryRepository entries,
    IAccountRepository accounts) : IQueryHandler<GetTrialBalanceQuery, TrialBalanceDto>
{
    public async Task<ErrorOr<TrialBalanceDto>> Handle(
        GetTrialBalanceQuery query,
        CancellationToken cancellationToken = default)
    {
        var periodAccounts = await accounts.GetByPeriodAsync(query.PeriodId, cancellationToken);
        if (periodAccounts.Count == 0)
        {
            return DomainErrors.FiscalPeriods.NotFound;
        }

        var postedLines = await entries.GetPostedLinesAsync(query.PeriodId, cancellationToken);

        var totalsByAccount = postedLines
            .GroupBy(line => line.AccountId)
            .ToDictionary(
                g => g.Key,
                g => (Debits: g.Sum(l => l.Debit), Credits: g.Sum(l => l.Credit)));

        var lines = new List<TrialBalanceLineDto>();
        foreach (var account in periodAccounts.OrderBy(a => a.Number.Value, StringComparer.Ordinal))
        {
            var (debits, credits) = totalsByAccount.GetValueOrDefault(account.Id, (0m, 0m));
            lines.Add(new TrialBalanceLineDto(
                AccountId: account.Id,
                AccountNumber: account.Number.Value,
                AccountName: account.Name,
                AccountType: account.Type.ToString(),
                TotalDebits: debits,
                TotalCredits: credits,
                Balance: account.Type is AccountType.Asset or AccountType.Expense
                    ? debits - credits
                    : credits - debits));
        }

        return new TrialBalanceDto(
            PeriodId: query.PeriodId,
            Lines: lines,
            TotalDebits: lines.Sum(l => l.TotalDebits),
            TotalCredits: lines.Sum(l => l.TotalCredits));
    }
}
