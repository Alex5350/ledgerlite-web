using LedgerLite.Api.Extensions;
using LedgerLite.Application.Abstractions;
using LedgerLite.Application.Features.Budgets;

namespace LedgerLite.Api.Features.Budgets;

public sealed record SetBudgetRequest(Guid PeriodId, string Category, decimal LimitAmount, string Currency);

public sealed record SetBudgetResponse(Guid Id);

internal static class BudgetEndpoints
{
    public static RouteGroupBuilder MapBudgetEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/", SetBudget)
            .WithName("Budgets_Set")
            .WithSummary("Set a budget for a category in a fiscal period");

        group.MapGet("/", ListBudgets)
            .WithName("Budgets_List")
            .WithSummary("List budgets of a fiscal period");

        group.MapPost("/evaluate", EvaluateBudgets)
            .WithName("Budgets_Evaluate")
            .WithSummary("Re-evaluate budgets against posted spending (raises threshold alerts)");

        return group;
    }

    private static async Task<IResult> SetBudget(
        SetBudgetRequest request,
        ICommandHandler<SetBudgetCommand, SetBudgetResult> handler,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        var command = new SetBudgetCommand(request.PeriodId, request.Category, request.LimitAmount, request.Currency);

        var result = await handler.Handle(command, cancellationToken);

        return await result.ToResponseAsync(async created =>
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return TypedResults.Created($"/api/budgets/{created.Id}", new SetBudgetResponse(created.Id));
        });
    }

    private static async Task<IResult> ListBudgets(
        IQueryHandler<GetBudgetsQuery, IReadOnlyList<BudgetDto>> handler,
        Guid periodId,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new GetBudgetsQuery(periodId), cancellationToken);

        return result.ToResponse(TypedResults.Ok);
    }

    private static async Task<IResult> EvaluateBudgets(
        EvaluateBudgetsRequest request,
        ICommandHandler<EvaluateBudgetsCommand, IReadOnlyList<BudgetEvaluationDto>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new EvaluateBudgetsCommand(request.PeriodId), cancellationToken);

        // The use case saves and dispatches threshold events itself.
        return result.ToResponse(TypedResults.Ok);
    }

    public sealed record EvaluateBudgetsRequest(Guid PeriodId);
}
