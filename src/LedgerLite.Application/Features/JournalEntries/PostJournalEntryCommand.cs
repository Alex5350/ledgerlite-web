using ErrorOr;
using FluentValidation;
using LedgerLite.Application.Abstractions;
using LedgerLite.Application.Common;
using LedgerLite.Domain.Journal;

namespace LedgerLite.Application.Features.JournalEntries;

public sealed record PostJournalEntryLine(Guid AccountId, decimal Debit, decimal Credit);

public sealed record PostJournalEntryCommand(Guid PeriodId, string? Description, IReadOnlyList<PostJournalEntryLine> Lines);

public sealed record PostJournalEntryResult(Guid Id);

public sealed class PostJournalEntryValidator : AbstractValidator<PostJournalEntryCommand>
{
    public PostJournalEntryValidator()
    {
        RuleFor(x => x.PeriodId).NotEmpty();

        RuleFor(x => x.Description)
            .MaximumLength(500);

        RuleFor(x => x.Lines)
            .NotEmpty()
            .Must(lines => lines.Count >= 2)
            .WithMessage("A journal entry must have at least two lines.");

        RuleForEach(x => x.Lines)
            .ChildRules(line =>
            {
                line.RuleFor(l => l.AccountId).NotEmpty();
                line.RuleFor(l => l.Debit).GreaterThanOrEqualTo(0);
                line.RuleFor(l => l.Credit).GreaterThanOrEqualTo(0);
                line.RuleFor(l => new { l.Debit, l.Credit })
                    .Must(v => (v.Debit > 0) ^ (v.Credit > 0))
                    .WithMessage("Each journal entry line must have exactly one positive side (debit or credit).");
            });
    }
}

public sealed class PostJournalEntryHandler(
    IJournalEntryRepository entries,
    IFiscalPeriodRepository periods,
    IAccountRepository accounts,
    IUnitOfWork unitOfWork,
    IDomainEventDispatcher dispatcher,
    IValidator<PostJournalEntryCommand> validator) : ICommandHandler<PostJournalEntryCommand, PostJournalEntryResult>
{
    public async Task<ErrorOr<PostJournalEntryResult>> Handle(
        PostJournalEntryCommand command,
        CancellationToken cancellationToken = default)
    {
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ErrorsOrEmpty;
        }

        var period = await periods.GetByIdAsync(command.PeriodId, cancellationToken);
        if (period is null)
        {
            return DomainErrors.FiscalPeriods.NotFound;
        }

        if (!period.IsOpen)
        {
            return DomainErrors.FiscalPeriods.ClosedForPosting;
        }

        var accountIds = command.Lines.Select(l => l.AccountId).Distinct().ToList();
        foreach (var accountId in accountIds)
        {
            if (await accounts.GetByIdAsync(accountId, cancellationToken) is null)
            {
                return DomainErrors.JournalEntries.AccountNotFound(accountId);
            }
        }

        if (!JournalEntry.TryCreate(
                command.PeriodId,
                command.Description,
                DateTime.UtcNow,
                command.Lines.Select(l => new JournalEntryLineInput(l.AccountId, l.Debit, l.Credit)),
                out var entry,
                out var error))
        {
            return Error.Validation("JournalEntries.Invalid", error);
        }

        if (!entry.TryPost(period, out var postError))
        {
            return Error.Conflict("JournalEntries.CannotPost", postError);
        }

        await entries.AddAsync(entry, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await dispatcher.DispatchAsync(entry.PullEvents(), cancellationToken);

        return new PostJournalEntryResult(entry.Id);
    }
}
