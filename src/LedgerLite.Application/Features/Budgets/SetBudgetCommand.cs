using ErrorOr;
using FluentValidation;
using LedgerLite.Application.Abstractions;
using LedgerLite.Application.Common;
using LedgerLite.Domain.Budgets;
using LedgerLite.Domain.ValueObjects;

namespace LedgerLite.Application.Features.Budgets;

public sealed record SetBudgetCommand(Guid PeriodId, string Category, decimal LimitAmount, string Currency);

public sealed record SetBudgetResult(Guid Id);

public sealed class SetBudgetValidator : AbstractValidator<SetBudgetCommand>
{
    public SetBudgetValidator()
    {
        RuleFor(x => x.PeriodId).NotEmpty();

        RuleFor(x => x.Category)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.LimitAmount)
            .GreaterThan(0);

        RuleFor(x => x.Currency)
            .NotEmpty()
            .Length(3);
    }
}

public sealed class SetBudgetHandler(
    IBudgetRepository budgets,
    IFiscalPeriodRepository periods,
    IValidator<SetBudgetCommand> validator) : ICommandHandler<SetBudgetCommand, SetBudgetResult>
{
    public async Task<ErrorOr<SetBudgetResult>> Handle(
        SetBudgetCommand command,
        CancellationToken cancellationToken = default)
    {
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ErrorsOrEmpty;
        }

        if (await periods.GetByIdAsync(command.PeriodId, cancellationToken) is null)
        {
            return DomainErrors.FiscalPeriods.NotFound;
        }

        if (!Money.TryCreate(command.LimitAmount, command.Currency, out var limit, out var moneyError))
        {
            return Error.Validation("Budgets.InvalidMoney", moneyError);
        }

        if (await budgets.GetByCategoryAsync(command.PeriodId, command.Category, cancellationToken) is not null)
        {
            return DomainErrors.Budgets.AlreadyExistsForCategory;
        }

        if (!Budget.TryCreate(command.PeriodId, command.Category, limit, out var budget, out var error))
        {
            return Error.Validation("Budgets.Invalid", error);
        }

        await budgets.AddAsync(budget, cancellationToken);

        return new SetBudgetResult(budget.Id);
    }
}
