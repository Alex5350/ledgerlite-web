using ErrorOr;
using FluentValidation;
using LedgerLite.Application.Abstractions;
using LedgerLite.Application.Features.Accounts;
using LedgerLite.Application.Features.Budgets;
using LedgerLite.Application.Features.FiscalPeriods;
using LedgerLite.Application.Features.JournalEntries;
using LedgerLite.Application.Features.Users;
using Microsoft.Extensions.DependencyInjection;

namespace LedgerLite.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, includeInternalTypes: true);

        services.ScanHandlers();

        return services;
    }

    private static IServiceCollection ScanHandlers(this IServiceCollection services) => services
        .AddScoped<ICommandHandler<RegisterUserCommand, RegisterUserResult>, RegisterUserHandler>()
        .AddScoped<ICommandHandler<CreateFiscalPeriodCommand, CreateFiscalPeriodResult>, CreateFiscalPeriodHandler>()
        .AddScoped<ICommandHandler<CloseFiscalPeriodCommand, Success>, CloseFiscalPeriodHandler>()
        .AddScoped<ICommandHandler<CreateAccountCommand, CreateAccountResult>, CreateAccountHandler>()
        .AddScoped<IQueryHandler<GetAccountByIdQuery, AccountDto>, GetAccountByIdHandler>()
        .AddScoped<ICommandHandler<PostJournalEntryCommand, PostJournalEntryResult>, PostJournalEntryHandler>()
        .AddScoped<IQueryHandler<GetJournalEntriesQuery, PagedResult<JournalEntryDto>>, GetJournalEntriesHandler>()
        .AddScoped<IQueryHandler<GetTrialBalanceQuery, TrialBalanceDto>, GetTrialBalanceHandler>()
        .AddScoped<ICommandHandler<SetBudgetCommand, SetBudgetResult>, SetBudgetHandler>()
        .AddScoped<IQueryHandler<GetBudgetsQuery, IReadOnlyList<BudgetDto>>, GetBudgetsHandler>()
        .AddScoped<ICommandHandler<EvaluateBudgetsCommand, IReadOnlyList<BudgetEvaluationDto>>, EvaluateBudgetsHandler>();
}
