using ErrorOr;
using FluentValidation;
using LedgerLite.Application.Abstractions;
using LedgerLite.Application.Common;
using LedgerLite.Domain.FiscalPeriods;

namespace LedgerLite.Application.Features.FiscalPeriods;

public sealed record CloseFiscalPeriodCommand(Guid PeriodId);

public sealed class CloseFiscalPeriodHandler(IFiscalPeriodRepository periods)
    : ICommandHandler<CloseFiscalPeriodCommand, Success>
{
    public async Task<ErrorOr<Success>> Handle(
        CloseFiscalPeriodCommand command,
        CancellationToken cancellationToken = default)
    {
        var period = await periods.GetByIdAsync(command.PeriodId, cancellationToken);
        if (period is null)
        {
            return DomainErrors.FiscalPeriods.NotFound;
        }

        // DateOnly.FromDateTime uses the local clock; derive from UTC to stay deterministic.
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        return period.TryClose(today, out var error)
            ? Result.Success
            : Error.Conflict("FiscalPeriods.CannotClose", error);
    }
}
