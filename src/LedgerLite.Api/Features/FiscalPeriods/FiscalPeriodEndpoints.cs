using ErrorOr;
using LedgerLite.Api.Extensions;
using LedgerLite.Application.Abstractions;
using LedgerLite.Application.Features.FiscalPeriods;
using LedgerLite.Domain.FiscalPeriods;

namespace LedgerLite.Api.Features.FiscalPeriods;

public sealed record CreateFiscalPeriodRequest(string Name, DateOnly StartDate, DateOnly EndDate);

public sealed record CreateFiscalPeriodResponse(Guid Id);

public sealed record FiscalPeriodResponse(Guid Id, string Name, DateOnly StartDate, DateOnly EndDate, string Status);

internal static class FiscalPeriodEndpoints
{
    public static RouteGroupBuilder MapFiscalPeriodEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/", CreatePeriod)
            .WithName("Periods_Create")
            .WithSummary("Create a fiscal period");

        group.MapPost("/{id:guid}/close", ClosePeriod)
            .WithName("Periods_Close")
            .WithSummary("Close a fiscal period");

        group.MapGet("/", ListPeriods)
            .WithName("Periods_List")
            .WithSummary("List all fiscal periods");

        return group;
    }

    private static async Task<IResult> CreatePeriod(
        CreateFiscalPeriodRequest request,
        ICommandHandler<CreateFiscalPeriodCommand, CreateFiscalPeriodResult> handler,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        var command = new CreateFiscalPeriodCommand(request.Name, request.StartDate, request.EndDate);

        var result = await handler.Handle(command, cancellationToken);

        return await result.ToResponseAsync(async created =>
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return TypedResults.Created($"/api/periods/{created.Id}", new CreateFiscalPeriodResponse(created.Id));
        });
    }

    private static async Task<IResult> ClosePeriod(
        Guid id,
        ICommandHandler<CloseFiscalPeriodCommand, Success> handler,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new CloseFiscalPeriodCommand(id), cancellationToken);

        return await result.ToResponseAsync(async _ =>
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return TypedResults.NoContent();
        });
    }

    private static async Task<IResult> ListPeriods(
        IFiscalPeriodRepository periods,
        CancellationToken cancellationToken)
    {
        var items = await periods.GetAllAsync(cancellationToken);

        return TypedResults.Ok(items.Select(ToResponse).ToList());
    }

    private static FiscalPeriodResponse ToResponse(FiscalPeriod period) => new(
        period.Id,
        period.Name,
        period.StartDate,
        period.EndDate,
        period.Status.ToString());
}
