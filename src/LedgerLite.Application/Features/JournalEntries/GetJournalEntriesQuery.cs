using ErrorOr;
using FluentValidation;
using LedgerLite.Application.Abstractions;
using LedgerLite.Application.Common;
using LedgerLite.Domain.Journal;

namespace LedgerLite.Application.Features.JournalEntries;

public sealed record GetJournalEntriesQuery(Guid? PeriodId = null, int Page = 1, int PageSize = 20);

public sealed record JournalEntryLineDto(Guid AccountId, decimal Debit, decimal Credit);

public sealed record JournalEntryDto(
    Guid Id,
    Guid FiscalPeriodId,
    string? Description,
    DateTime OccurredOnUtc,
    bool IsPosted,
    IReadOnlyList<JournalEntryLineDto> Lines);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);

public sealed class GetJournalEntriesValidator : AbstractValidator<GetJournalEntriesQuery>
{
    public GetJournalEntriesValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

public sealed class GetJournalEntriesHandler(
    IJournalEntryRepository entries,
    IValidator<GetJournalEntriesQuery> validator) : IQueryHandler<GetJournalEntriesQuery, PagedResult<JournalEntryDto>>
{
    public async Task<ErrorOr<PagedResult<JournalEntryDto>>> Handle(
        GetJournalEntriesQuery query,
        CancellationToken cancellationToken = default)
    {
        var validation = await validator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ErrorsOrEmpty;
        }

        var (items, totalCount) = await entries.GetPagedAsync(query.PeriodId, query.Page, query.PageSize, cancellationToken);

        var dtos = items
            .Select(e => new JournalEntryDto(
                Id: e.Id,
                FiscalPeriodId: e.FiscalPeriodId,
                Description: e.Description,
                OccurredOnUtc: e.OccurredOn,
                IsPosted: e.IsPosted,
                Lines: [.. e.Lines.Select(l => new JournalEntryLineDto(l.AccountId, l.Debit, l.Credit))]))
            .ToList();

        return new PagedResult<JournalEntryDto>(dtos, totalCount, query.Page, query.PageSize);
    }
}
