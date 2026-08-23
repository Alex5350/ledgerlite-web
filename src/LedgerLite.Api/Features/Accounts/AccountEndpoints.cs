using LedgerLite.Api.Extensions;
using LedgerLite.Application.Abstractions;
using LedgerLite.Application.Features.Accounts;
using LedgerLite.Domain.Accounts;
using Account = LedgerLite.Domain.Accounts.Account;

namespace LedgerLite.Api.Features.Accounts;

public sealed record CreateAccountRequest(string Number, string Name, AccountType Type, Guid PeriodId);

public sealed record CreateAccountResponse(Guid Id);

internal static class AccountEndpoints
{
    public static RouteGroupBuilder MapAccountEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/", CreateAccount)
            .WithName("Accounts_Create")
            .WithSummary("Create a ledger account in a fiscal period");

        group.MapGet("/{id:guid}", GetAccountById)
            .WithName("Accounts_GetById")
            .WithSummary("Get an account by id");

        group.MapGet("/", ListByPeriod)
            .WithName("Accounts_ListByPeriod")
            .WithSummary("List accounts of a fiscal period");

        return group;
    }

    private static async Task<IResult> CreateAccount(
        CreateAccountRequest request,
        ICommandHandler<CreateAccountCommand, CreateAccountResult> handler,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        var command = new CreateAccountCommand(request.Number, request.Name, request.Type, request.PeriodId);

        var result = await handler.Handle(command, cancellationToken);

        return await result.ToResponseAsync(async created =>
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return TypedResults.Created($"/api/accounts/{created.Id}", new CreateAccountResponse(created.Id));
        });
    }

    private static async Task<IResult> GetAccountById(
        Guid id,
        IQueryHandler<GetAccountByIdQuery, AccountDto> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new GetAccountByIdQuery(id), cancellationToken);

        return result.ToResponse(TypedResults.Ok);
    }

    private static async Task<IResult> ListByPeriod(
        Guid periodId,
        IAccountRepository accounts,
        CancellationToken cancellationToken)
    {
        var items = await accounts.GetByPeriodAsync(periodId, cancellationToken);

        return TypedResults.Ok(items.Select(ToResponse).ToList());
    }

    private static AccountDto ToResponse(Account account) => new(
        account.Id,
        account.Number.Value,
        account.Name,
        account.Type.ToString(),
        account.FiscalPeriodId);
}
