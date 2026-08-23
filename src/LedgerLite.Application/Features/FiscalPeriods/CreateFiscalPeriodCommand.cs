using ErrorOr;
using FluentValidation;
using LedgerLite.Application.Abstractions;
using LedgerLite.Application.Common;
using LedgerLite.Domain.FiscalPeriods;

namespace LedgerLite.Application.Features.FiscalPeriods;

public sealed record CreateFiscalPeriodCommand(string Name, DateOnly StartDate, DateOnly EndDate);

public sealed record CreateFiscalPeriodResult(Guid Id);

public sealed class CreateFiscalPeriodValidator : AbstractValidator<CreateFiscalPeriodCommand>
{
    public CreateFiscalPeriodValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.EndDate)
            .GreaterThanOrEqualTo(x => x.StartDate)
            .WithMessage("Fiscal period end date must not be before its start date.");
    }
}

public sealed class CreateFiscalPeriodHandler(
    IFiscalPeriodRepository periods,
    IValidator<CreateFiscalPeriodCommand> validator) : ICommandHandler<CreateFiscalPeriodCommand, CreateFiscalPeriodResult>
{
    public async Task<ErrorOr<CreateFiscalPeriodResult>> Handle(
        CreateFiscalPeriodCommand command,
        CancellationToken cancellationToken = default)
    {
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ErrorsOrEmpty;
        }

        if (!FiscalPeriod.TryCreate(command.Name, command.StartDate, command.EndDate, out var period, out var error))
        {
            return Error.Validation("FiscalPeriods.Invalid", error);
        }

        await periods.AddAsync(period, cancellationToken);

        return new CreateFiscalPeriodResult(period.Id);
    }
}
