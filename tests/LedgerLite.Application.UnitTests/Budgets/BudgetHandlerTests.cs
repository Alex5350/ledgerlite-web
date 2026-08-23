using ErrorOr;
using LedgerLite.Application.Abstractions;
using LedgerLite.Application.Common;
using LedgerLite.Application.Features.Budgets;
using LedgerLite.Domain.Budgets;
using LedgerLite.Domain.Events;
using LedgerLite.Domain.FiscalPeriods;
using LedgerLite.Domain.Specifications;

namespace LedgerLite.Application.UnitTests.Budgets;

public sealed class SetBudgetHandlerTests
{
    private readonly IBudgetRepository _budgets = Substitute.For<IBudgetRepository>();
    private readonly IFiscalPeriodRepository _periods = Substitute.For<IFiscalPeriodRepository>();
    private readonly SetBudgetHandler _handler;

    public SetBudgetHandlerTests()
    {
        _handler = new SetBudgetHandler(_budgets, _periods, new SetBudgetValidator());
    }

    private SetBudgetCommand ValidCommand(Guid? periodId = null) =>
        new(periodId ?? Guid.CreateVersion7(), "  Groceries  ", 500m, "usd");

    [Fact]
    public async Task Handle_WithValidCommand_AddsBudgetWithNormalizedLimit()
    {
        var periodId = Guid.CreateVersion7();
        _periods.GetByIdAsync(periodId, Arg.Any<CancellationToken>()).Returns(TestDomain.OpenPeriod());
        _budgets.GetByCategoryAsync(periodId, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((Budget?)null);
        Budget? added = null;
        _ = _budgets.AddAsync(Arg.Do<Budget>(budget => added = budget), Arg.Any<CancellationToken>());

        var result = await _handler.Handle(ValidCommand(periodId));

        Assert.False(result.IsError);
        Assert.NotNull(added);
        Assert.Equal(added!.Id, result.Value.Id);
        Assert.Equal("Groceries", added.Category);
        Assert.Equal(500m, added.Limit.Amount);
        Assert.Equal("USD", added.Limit.Currency);
        await _budgets.Received(1).AddAsync(Arg.Any<Budget>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_WithMissingCategory_ReturnsValidationError(string? category)
    {
        var command = new SetBudgetCommand(Guid.CreateVersion7(), category!, 500m, "USD");

        var result = await _handler.Handle(command);

        Assert.Equal("Category", result.FirstErrorOrThrow().Code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Handle_WithNonPositiveLimit_ReturnsValidationError(decimal limitAmount)
    {
        var command = new SetBudgetCommand(Guid.CreateVersion7(), "Groceries", limitAmount, "USD");

        var result = await _handler.Handle(command);

        Assert.Equal("LimitAmount", result.FirstErrorOrThrow().Code);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("US")]
    [InlineData("USDD")]
    public async Task Handle_WithInvalidCurrency_ReturnsValidationError(string? currency)
    {
        var command = new SetBudgetCommand(Guid.CreateVersion7(), "Groceries", 500m, currency!);

        var result = await _handler.Handle(command);

        Assert.Equal("Currency", result.FirstErrorOrThrow().Code);
    }

    [Fact]
    public async Task Handle_WithEmptyPeriodId_ReturnsValidationError()
    {
        var result = await _handler.Handle(ValidCommand(periodId: Guid.Empty));

        Assert.Equal("PeriodId", result.FirstErrorOrThrow().Code);
    }

    [Fact]
    public async Task Handle_WhenPeriodIsMissing_ReturnsNotFound()
    {
        var periodId = Guid.CreateVersion7();
        _periods.GetByIdAsync(periodId, Arg.Any<CancellationToken>()).Returns((FiscalPeriod?)null);

        var result = await _handler.Handle(ValidCommand(periodId));

        var error = result.FirstErrorOrThrow();
        Assert.Equal(ErrorType.NotFound, error.Type);
        Assert.Equal(DomainErrors.FiscalPeriods.NotFound.Code, error.Code);
    }

    [Fact]
    public async Task Handle_WithSubCentPrecisionLimit_ReturnsInvalidMoneyError()
    {
        var periodId = Guid.CreateVersion7();
        _periods.GetByIdAsync(periodId, Arg.Any<CancellationToken>()).Returns(TestDomain.OpenPeriod());
        var command = new SetBudgetCommand(periodId, "Groceries", 10.567m, "USD");

        var result = await _handler.Handle(command);

        var error = result.FirstErrorOrThrow();
        Assert.Equal(ErrorType.Validation, error.Type);
        Assert.Equal(DomainErrors.Budgets.InvalidMoney.Code, error.Code);
    }

    [Fact]
    public async Task Handle_WhenCategoryAlreadyBudgeted_ReturnsConflict()
    {
        var periodId = Guid.CreateVersion7();
        _periods.GetByIdAsync(periodId, Arg.Any<CancellationToken>()).Returns(TestDomain.OpenPeriod());
        _budgets.GetByCategoryAsync(periodId, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(TestDomain.NewBudget(periodId));

        var result = await _handler.Handle(ValidCommand(periodId));

        var error = result.FirstErrorOrThrow();
        Assert.Equal(ErrorType.Conflict, error.Type);
        Assert.Equal(DomainErrors.Budgets.AlreadyExistsForCategory.Code, error.Code);
        await _budgets.DidNotReceive().AddAsync(Arg.Any<Budget>(), Arg.Any<CancellationToken>());
    }
}

public sealed class GetBudgetsHandlerTests
{
    private readonly IBudgetRepository _budgets = Substitute.For<IBudgetRepository>();

    [Fact]
    public async Task Handle_MapsBudgetsToDtos()
    {
        var periodId = Guid.CreateVersion7();
        var groceries = TestDomain.NewBudget(periodId, "Groceries", 500m);
        var travel = TestDomain.NewBudget(periodId, "Travel", 1200m);
        _budgets.GetByPeriodAsync(periodId, Arg.Any<CancellationToken>()).Returns([groceries, travel]);
        var handler = new GetBudgetsHandler(_budgets);

        var result = await handler.Handle(new GetBudgetsQuery(periodId));

        Assert.False(result.IsError);
        Assert.Equal(2, result.Value.Count);
        Assert.Equal(groceries.Id, result.Value[0].Id);
        Assert.Equal("Groceries", result.Value[0].Category);
        Assert.Equal(500m, result.Value[0].LimitAmount);
        Assert.Equal("USD", result.Value[0].Currency);
        Assert.Equal(periodId, result.Value[0].FiscalPeriodId);
        Assert.Equal(1200m, result.Value[1].LimitAmount);
    }

    [Fact]
    public async Task Handle_WhenPeriodHasNoBudgets_ReturnsEmptyList()
    {
        _budgets.GetByPeriodAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns([]);
        var handler = new GetBudgetsHandler(_budgets);

        var result = await handler.Handle(new GetBudgetsQuery(Guid.CreateVersion7()));

        Assert.False(result.IsError);
        Assert.Empty(result.Value);
    }
}

public sealed class EvaluateBudgetsHandlerTests
{
    private readonly IBudgetRepository _budgets = Substitute.For<IBudgetRepository>();
    private readonly IJournalEntryRepository _entries = Substitute.For<IJournalEntryRepository>();
    private readonly IAccountRepository _accounts = Substitute.For<IAccountRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IDomainEventDispatcher _dispatcher = Substitute.For<IDomainEventDispatcher>();
    private readonly EvaluateBudgetsHandler _handler;

    public EvaluateBudgetsHandlerTests()
    {
        _handler = new EvaluateBudgetsHandler(_budgets, _entries, _accounts, _unitOfWork, _dispatcher);
    }

    [Fact]
    public async Task Handle_WhenPeriodHasNoBudgets_ReturnsNotFound()
    {
        var periodId = Guid.CreateVersion7();
        _budgets.GetByPeriodAsync(periodId, Arg.Any<CancellationToken>()).Returns([]);

        var result = await _handler.Handle(new EvaluateBudgetsCommand(periodId));

        var error = result.FirstErrorOrThrow();
        Assert.Equal(ErrorType.NotFound, error.Type);
        Assert.Equal(DomainErrors.Budgets.NotFound.Code, error.Code);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenSpendingCrossesEightyPercent_ReportsThresholdSavesAndDispatches()
    {
        var periodId = Guid.CreateVersion7();
        var budget = TestDomain.NewBudget(periodId, "Marketing", 100m);
        var marketingAccount = TestDomain.NewAccount("5010", "Marketing", Domain.Accounts.AccountType.Expense, periodId);
        _budgets.GetByPeriodAsync(periodId, Arg.Any<CancellationToken>()).Returns([budget]);
        _accounts.GetSatisfyingAsync(Arg.Any<ISpecification<Domain.Accounts.Account>>(), Arg.Any<CancellationToken>())
            .Returns([marketingAccount]);
        _entries.GetPostedLinesAsync(periodId, Arg.Any<CancellationToken>())
            .Returns([(marketingAccount.Id, 80m, 0m)]);
        IReadOnlyList<Domain.Common.IDomainEvent>? dispatched = null;
        _ = _dispatcher.DispatchAsync(
            Arg.Do<IEnumerable<Domain.Common.IDomainEvent>>(events => dispatched = events.ToList()),
            Arg.Any<CancellationToken>());

        var result = await _handler.Handle(new EvaluateBudgetsCommand(periodId));

        Assert.False(result.IsError);
        var evaluation = Assert.Single(result.Value);
        Assert.Equal(budget.Id, evaluation.BudgetId);
        Assert.Equal("Marketing", evaluation.Category);
        Assert.Equal(80m, evaluation.SpentAmount);
        Assert.Equal("EightyPercent", Assert.Single(evaluation.ThresholdsExceeded));
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        var raised = Assert.IsType<BudgetThresholdExceededDomainEvent>(Assert.Single(dispatched!));
        Assert.Equal(BudgetThreshold.EightyPercent, raised.Threshold);
        Assert.Equal("Marketing", raised.Category);
    }

    [Fact]
    public async Task Handle_OnReEvaluationWithSameSpending_DoesNotReportDuplicateThreshold()
    {
        var periodId = Guid.CreateVersion7();
        var budget = TestDomain.NewBudget(periodId, "Marketing", 100m);
        var marketingAccount = TestDomain.NewAccount("5010", "Marketing", Domain.Accounts.AccountType.Expense, periodId);
        _budgets.GetByPeriodAsync(periodId, Arg.Any<CancellationToken>()).Returns([budget]);
        _accounts.GetSatisfyingAsync(Arg.Any<ISpecification<Domain.Accounts.Account>>(), Arg.Any<CancellationToken>())
            .Returns([marketingAccount]);
        _entries.GetPostedLinesAsync(periodId, Arg.Any<CancellationToken>())
            .Returns([(marketingAccount.Id, 80m, 0m)]);

        await _handler.Handle(new EvaluateBudgetsCommand(periodId));
        var secondResult = await _handler.Handle(new EvaluateBudgetsCommand(periodId));

        Assert.False(secondResult.IsError);
        Assert.Empty(Assert.Single(secondResult.Value).ThresholdsExceeded);
        await _dispatcher.Received(2).DispatchAsync(Arg.Any<IEnumerable<Domain.Common.IDomainEvent>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenSpendingCrossesHundredPercent_ReportsHundredPercent()
    {
        var periodId = Guid.CreateVersion7();
        var budget = TestDomain.NewBudget(periodId, "Marketing", 100m);
        var marketingAccount = TestDomain.NewAccount("5010", "Marketing", Domain.Accounts.AccountType.Expense, periodId);
        _budgets.GetByPeriodAsync(periodId, Arg.Any<CancellationToken>()).Returns([budget]);
        _accounts.GetSatisfyingAsync(Arg.Any<ISpecification<Domain.Accounts.Account>>(), Arg.Any<CancellationToken>())
            .Returns([marketingAccount]);
        _entries.GetPostedLinesAsync(periodId, Arg.Any<CancellationToken>())
            .Returns([(marketingAccount.Id, 100m, 0m)]);

        var result = await _handler.Handle(new EvaluateBudgetsCommand(periodId));

        Assert.Equal("HundredPercent", Assert.Single(Assert.Single(result.Value).ThresholdsExceeded));
    }

    [Fact]
    public async Task Handle_WhenNoAccountMatchesCategory_ReportsZeroSpending()
    {
        var periodId = Guid.CreateVersion7();
        var budget = TestDomain.NewBudget(periodId, "Travel", 100m);
        _budgets.GetByPeriodAsync(periodId, Arg.Any<CancellationToken>()).Returns([budget]);
        _accounts.GetSatisfyingAsync(Arg.Any<ISpecification<Domain.Accounts.Account>>(), Arg.Any<CancellationToken>())
            .Returns([]);
        _entries.GetPostedLinesAsync(periodId, Arg.Any<CancellationToken>()).Returns([]);

        var result = await _handler.Handle(new EvaluateBudgetsCommand(periodId));

        Assert.False(result.IsError);
        var evaluation = Assert.Single(result.Value);
        Assert.Equal(0m, evaluation.SpentAmount);
        Assert.Empty(evaluation.ThresholdsExceeded);
    }
}
