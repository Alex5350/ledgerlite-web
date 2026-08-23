using ErrorOr;
using FluentValidation;
using LedgerLite.Application.Abstractions;
using LedgerLite.Application.Common;
using LedgerLite.Domain.Accounts;
using LedgerLite.Domain.ValueObjects;

namespace LedgerLite.Application.Features.Accounts;

public sealed record CreateAccountCommand(string Number, string Name, AccountType Type, Guid PeriodId);

public sealed record CreateAccountResult(Guid Id);

public sealed class CreateAccountValidator : AbstractValidator<CreateAccountCommand>
{
    public CreateAccountValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.PeriodId)
            .NotEmpty();
    }
}

public sealed class CreateAccountHandler(
    IAccountRepository accounts,
    IFiscalPeriodRepository periods,
    IValidator<CreateAccountCommand> validator) : ICommandHandler<CreateAccountCommand, CreateAccountResult>
{
    public async Task<ErrorOr<CreateAccountResult>> Handle(
        CreateAccountCommand command,
        CancellationToken cancellationToken = default)
    {
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ErrorsOrEmpty;
        }

        if (!AccountNumber.TryCreate(command.Number, out var number, out _))
        {
            return DomainErrors.Accounts.InvalidNumber;
        }

        if (await periods.GetByIdAsync(command.PeriodId, cancellationToken) is null)
        {
            return DomainErrors.FiscalPeriods.NotFound;
        }

        if (await accounts.NumberExistsInPeriodAsync(number, command.PeriodId, cancellationToken))
        {
            return DomainErrors.Accounts.NumberTaken;
        }

        if (!Account.TryCreate(number, command.Name, command.Type, command.PeriodId, out var account, out var error))
        {
            return Error.Validation("Accounts.Invalid", error);
        }

        await accounts.AddAsync(account, cancellationToken);

        return new CreateAccountResult(account.Id);
    }
}
