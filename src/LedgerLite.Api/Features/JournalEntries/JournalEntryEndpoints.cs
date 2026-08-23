using LedgerLite.Api.Extensions;
using LedgerLite.Application.Abstractions;
using LedgerLite.Application.Features.JournalEntries;

namespace LedgerLite.Api.Features.JournalEntries;

public sealed record PostJournalEntryLineRequest(Guid AccountId, decimal Debit, decimal Credit);

public sealed record PostJournalEntryRequest(Guid PeriodId, string? Description, IReadOnlyList<PostJournalEntryLineRequest> Lines);

public sealed record PostJournalEntryResponse(Guid Id);

internal static class JournalEntryEndpoints
{
    public static RouteGroupBuilder MapJournalEntryEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/", PostEntry)
            .WithName("JournalEntries_Post")
            .WithSummary("Post a balanced journal entry");

        group.MapGet("/", GetEntries)
            .WithName("JournalEntries_List")
            .WithSummary("List journal entries (paged, optionally filtered by period)");

        return group;
    }

    private static async Task<IResult> PostEntry(
        PostJournalEntryRequest request,
        ICommandHandler<PostJournalEntryCommand, PostJournalEntryResult> handler,
        CancellationToken cancellationToken)
    {
        var command = new PostJournalEntryCommand(
            request.PeriodId,
            request.Description,
            [.. request.Lines.Select(line => new PostJournalEntryLine(line.AccountId, line.Debit, line.Credit))]);

        var result = await handler.Handle(command, cancellationToken);

        // The use case saves and dispatches domain events itself.
        return result.ToResponse(posted =>
            TypedResults.Created($"/api/journal-entries/{posted.Id}", new PostJournalEntryResponse(posted.Id)));
    }

    private static async Task<IResult> GetEntries(
        IQueryHandler<GetJournalEntriesQuery, PagedResult<JournalEntryDto>> handler,
        Guid? periodId,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await handler.Handle(
            new GetJournalEntriesQuery(periodId, page, pageSize),
            cancellationToken);

        return result.ToResponse(TypedResults.Ok);
    }
}
