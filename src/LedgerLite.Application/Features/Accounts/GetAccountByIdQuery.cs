using ErrorOr;
using LedgerLite.Application.Abstractions;
using LedgerLite.Application.Common;
using LedgerLite.Domain.Accounts;

namespace LedgerLite.Application.Features.Accounts;

public sealed record GetAccountByIdQuery(Guid AccountId);

public sealed record AccountDto(Guid Id, string Number, string Name, string Type, Guid FiscalPeriodId);

public sealed class GetAccountByIdHandler(IAccountRepository accounts)
    : IQueryHandler<GetAccountByIdQuery, AccountDto>
{
    public async Task<ErrorOr<AccountDto>> Handle(
        GetAccountByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        var account = await accounts.GetByIdAsync(query.AccountId, cancellationToken);
        return account is null
            ? DomainErrors.Accounts.NotFound
            : new AccountDto(
                Id: account.Id,
                Number: account.Number.Value,
                Name: account.Name,
                Type: account.Type.ToString(),
                FiscalPeriodId: account.FiscalPeriodId);
    }
}
