using LedgerLite.Api.Extensions;
using LedgerLite.Application.Abstractions;
using LedgerLite.Application.Features.JournalEntries;

namespace LedgerLite.Api.Features.Reports;

internal static class ReportEndpoints
{
    public static RouteGroupBuilder MapReportEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/trial-balance", GetTrialBalance)
            .WithName("Reports_TrialBalance")
            .WithSummary("Trial balance of a fiscal period (posted entries only)");

        return group;
    }

    private static async Task<IResult> GetTrialBalance(
        IQueryHandler<GetTrialBalanceQuery, TrialBalanceDto> handler,
        Guid periodId,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new GetTrialBalanceQuery(periodId), cancellationToken);

        return result.ToResponse(TypedResults.Ok);
    }
}
