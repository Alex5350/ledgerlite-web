using ErrorOr;
using LedgerLite.Application.Abstractions;
using LedgerLite.Application.Common;
using LedgerLite.Application.Features.JournalEntries;
using LedgerLite.Domain.Events;
using LedgerLite.Domain.FiscalPeriods;
using LedgerLite.Domain.Journal;

namespace LedgerLite.Application.UnitTests.JournalEntries;

public sealed class PostJournalEntryHandlerTests
{
    private readonly IJournalEntryRepository _entries = Substitute.For<IJournalEntryRepository>();
    private readonly IFiscalPeriodRepository _periods = Substitute.For<IFiscalPeriodRepository>();
    private readonly IAccountRepository _accounts = Substitute.For<IAccountRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IDomainEventDispatcher _dispatcher = Substitute.For<IDomainEventDispatcher>();
    private readonly PostJournalEntryHandler _handler;

    public PostJournalEntryHandlerTests()
    {
        _handler = new PostJournalEntryHandler(
            _entries,
            _periods,
            _accounts,
            _unitOfWork,
            _dispatcher,
            new PostJournalEntryValidator());
    }

    private static FiscalPeriod OpenPeriod()
    {
        var ok = FiscalPeriod.TryCreate("Test period", new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30), out var period, out var error);
        return ok ? period! : throw new InvalidOperationException(error);
    }

    private PostJournalEntryCommand BalancedCommand(Guid periodId, Guid debitAccountId, Guid creditAccountId, decimal amount = 100m) =>
        new(periodId, "Test entry",
        [
            new PostJournalEntryLine(debitAccountId, amount, 0m),
            new PostJournalEntryLine(creditAccountId, 0m, amount)
        ]);

    [Fact]
    public async Task Handle_WithBalancedEntry_AddsSavesAndDispatchesPostedEvent()
    {
        var period = OpenPeriod();
        var debitAccount = TestDomain.NewAccount();
        var creditAccount = TestDomain.NewAccount(number: "3010");
        _periods.GetByIdAsync(period.Id, Arg.Any<CancellationToken>()).Returns(period);
        _accounts.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(debitAccount);
        JournalEntry? added = null;
        _ = _entries.AddAsync(Arg.Do<JournalEntry>(entry => added = entry), Arg.Any<CancellationToken>());
        IReadOnlyList<Domain.Common.IDomainEvent>? dispatched = null;
        _ = _dispatcher.DispatchAsync(
            Arg.Do<IEnumerable<Domain.Common.IDomainEvent>>(events => dispatched = events.ToList()),
            Arg.Any<CancellationToken>());

        var result = await _handler.Handle(BalancedCommand(period.Id, debitAccount.Id, creditAccount.Id));

        Assert.False(result.IsError);
        Assert.NotNull(added);
        Assert.Equal(result.Value.Id, added!.Id);
        Assert.True(added.IsPosted);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _dispatcher.Received(1).DispatchAsync(Arg.Any<IEnumerable<Domain.Common.IDomainEvent>>(), Arg.Any<CancellationToken>());
        var postedEvent = Assert.IsType<JournalEntryPostedDomainEvent>(Assert.Single(dispatched!));
        Assert.Equal(added.Id, postedEvent.EntryId);
        Assert.Equal(period.Id, postedEvent.FiscalPeriodId);
    }

    [Fact]
    public async Task Handle_HappyPath_SavesBeforeDispatchingEvents()
    {
        var period = OpenPeriod();
        _periods.GetByIdAsync(period.Id, Arg.Any<CancellationToken>()).Returns(period);
        _accounts.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(TestDomain.NewAccount());
        var order = new List<string>();
        _ = _entries.AddAsync(Arg.Do<JournalEntry>(_ => order.Add("add")), Arg.Any<CancellationToken>());
        _ = _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                order.Add("save");
                return 1;
            });
        _ = _dispatcher.DispatchAsync(
            Arg.Do<IEnumerable<Domain.Common.IDomainEvent>>(_ => order.Add("dispatch")),
            Arg.Any<CancellationToken>());

        await _handler.Handle(BalancedCommand(period.Id, Guid.CreateVersion7(), Guid.CreateVersion7()));

        Assert.Equal(["add", "save", "dispatch"], order);
    }

    [Fact]
    public async Task Handle_WithEmptyPeriodId_ReturnsValidationError()
    {
        var result = await _handler.Handle(BalancedCommand(Guid.Empty, Guid.CreateVersion7(), Guid.CreateVersion7()));

        var error = result.FirstErrorOrThrow();
        Assert.Equal(ErrorType.Validation, error.Type);
        Assert.Equal("PeriodId", error.Code);
    }

    [Fact]
    public async Task Handle_WithNoLines_ReturnsValidationError()
    {
        var command = new PostJournalEntryCommand(Guid.CreateVersion7(), null, []);

        var result = await _handler.Handle(command);

        Assert.Equal(ErrorType.Validation, result.FirstErrorOrThrow().Type);
    }

    [Fact]
    public async Task Handle_WithSingleLine_ReturnsValidationError()
    {
        var command = new PostJournalEntryCommand(Guid.CreateVersion7(), null,
            [new PostJournalEntryLine(Guid.CreateVersion7(), 100m, 0m)]);

        var result = await _handler.Handle(command);

        var error = result.FirstErrorOrThrow();
        Assert.Equal(ErrorType.Validation, error.Type);
        Assert.Contains("at least two lines", error.Description);
    }

    [Fact]
    public async Task Handle_WithLineHavingBothSidesPositive_ReturnsValidationError()
    {
        var command = new PostJournalEntryCommand(Guid.CreateVersion7(), null,
        [
            new PostJournalEntryLine(Guid.CreateVersion7(), 100m, 100m),
            new PostJournalEntryLine(Guid.CreateVersion7(), 0m, 200m)
        ]);

        var result = await _handler.Handle(command);

        var error = result.FirstErrorOrThrow();
        Assert.Equal(ErrorType.Validation, error.Type);
        Assert.Contains("exactly one positive side", error.Description);
    }

    [Fact]
    public async Task Handle_WithNegativeAmount_ReturnsValidationError()
    {
        var command = new PostJournalEntryCommand(Guid.CreateVersion7(), null,
        [
            new PostJournalEntryLine(Guid.CreateVersion7(), -5m, 0m),
            new PostJournalEntryLine(Guid.CreateVersion7(), 0m, -5m)
        ]);

        var result = await _handler.Handle(command);

        var error = result.FirstErrorOrThrow();
        Assert.Equal(ErrorType.Validation, error.Type);
    }

    [Fact]
    public async Task Handle_WhenPeriodIsMissing_ReturnsNotFound()
    {
        var periodId = Guid.CreateVersion7();
        _periods.GetByIdAsync(periodId, Arg.Any<CancellationToken>()).Returns((FiscalPeriod?)null);

        var result = await _handler.Handle(BalancedCommand(periodId, Guid.CreateVersion7(), Guid.CreateVersion7()));

        var error = result.FirstErrorOrThrow();
        Assert.Equal(ErrorType.NotFound, error.Type);
        Assert.Equal(DomainErrors.FiscalPeriods.NotFound.Code, error.Code);
        await _entries.DidNotReceive().AddAsync(Arg.Any<JournalEntry>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenPeriodIsClosed_ReturnsConflictWithoutTouchingAccounts()
    {
        var period = TestDomain.ClosedPeriod();
        _periods.GetByIdAsync(period.Id, Arg.Any<CancellationToken>()).Returns(period);

        var result = await _handler.Handle(BalancedCommand(period.Id, Guid.CreateVersion7(), Guid.CreateVersion7()));

        var error = result.FirstErrorOrThrow();
        Assert.Equal(ErrorType.Conflict, error.Type);
        Assert.Equal(DomainErrors.FiscalPeriods.ClosedForPosting.Code, error.Code);
        await _accounts.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _entries.DidNotReceive().AddAsync(Arg.Any<JournalEntry>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenReferencedAccountIsMissing_ReturnsAccountNotFound()
    {
        var period = OpenPeriod();
        var missingAccountId = Guid.CreateVersion7();
        _periods.GetByIdAsync(period.Id, Arg.Any<CancellationToken>()).Returns(period);
        _accounts.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Domain.Accounts.Account?)null);

        var result = await _handler.Handle(BalancedCommand(period.Id, missingAccountId, Guid.CreateVersion7()));

        var error = result.FirstErrorOrThrow();
        Assert.Equal(ErrorType.NotFound, error.Type);
        Assert.Equal("JournalEntries.AccountNotFound", error.Code);
        Assert.Contains(missingAccountId.ToString(), error.Description);
    }

    [Fact]
    public async Task Handle_WithUnbalancedLines_ReturnsValidationError()
    {
        var period = OpenPeriod();
        _periods.GetByIdAsync(period.Id, Arg.Any<CancellationToken>()).Returns(period);
        _accounts.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(TestDomain.NewAccount());
        var command = new PostJournalEntryCommand(period.Id, null,
        [
            new PostJournalEntryLine(Guid.CreateVersion7(), 100m, 0m),
            new PostJournalEntryLine(Guid.CreateVersion7(), 0m, 90m)
        ]);

        var result = await _handler.Handle(command);

        var error = result.FirstErrorOrThrow();
        Assert.Equal(ErrorType.Validation, error.Type);
        Assert.Equal("JournalEntries.Invalid", error.Code);
        Assert.Contains("not balanced", error.Description);
        await _entries.DidNotReceive().AddAsync(Arg.Any<JournalEntry>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}

public sealed class GetJournalEntriesHandlerTests
{
    private readonly IJournalEntryRepository _entries = Substitute.For<IJournalEntryRepository>();
    private readonly GetJournalEntriesHandler _handler;

    public GetJournalEntriesHandlerTests()
    {
        _handler = new GetJournalEntriesHandler(_entries, new GetJournalEntriesValidator());
    }

    [Fact]
    public async Task Handle_WithPagedResults_MapsEntriesAndEchoesPaging()
    {
        var periodId = Guid.CreateVersion7();
        var first = TestDomain.NewEntry(periodId, (Guid.CreateVersion7(), 100m, 0m), (Guid.CreateVersion7(), 0m, 100m));
        var second = TestDomain.NewEntry(periodId, (Guid.CreateVersion7(), 50m, 0m), (Guid.CreateVersion7(), 0m, 50m));
        _entries.GetPagedAsync(Arg.Any<Guid?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(((IReadOnlyList<JournalEntry>)[first, second], TotalCount: 7));

        var result = await _handler.Handle(new GetJournalEntriesQuery(periodId, Page: 2, PageSize: 2));

        Assert.False(result.IsError);
        var page = result.Value;
        Assert.Equal(7, page.TotalCount);
        Assert.Equal(2, page.Page);
        Assert.Equal(2, page.PageSize);
        Assert.Equal(2, page.Items.Count);
        Assert.Equal(first.Id, page.Items[0].Id);
        Assert.Equal("Test entry", page.Items[0].Description);
        Assert.False(page.Items[0].IsPosted);
        Assert.Equal(2, page.Items[0].Lines.Count);
        await _entries.Received(1).GetPagedAsync(periodId, 2, 2, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Handle_WithNonPositivePage_ReturnsValidationError(int page)
    {
        var result = await _handler.Handle(new GetJournalEntriesQuery(null, page, 20));

        Assert.Equal("Page", result.FirstErrorOrThrow().Code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task Handle_WithOutOfRangePageSize_ReturnsValidationError(int pageSize)
    {
        var result = await _handler.Handle(new GetJournalEntriesQuery(null, 1, pageSize));

        Assert.Equal("PageSize", result.FirstErrorOrThrow().Code);
    }
}

public sealed class GetTrialBalanceHandlerTests
{
    private readonly IJournalEntryRepository _entries = Substitute.For<IJournalEntryRepository>();
    private readonly IAccountRepository _accounts = Substitute.For<IAccountRepository>();
    private readonly GetTrialBalanceHandler _handler;

    public GetTrialBalanceHandlerTests()
    {
        _handler = new GetTrialBalanceHandler(_entries, _accounts);
    }

    [Fact]
    public async Task Handle_WhenPeriodHasNoAccounts_ReturnsNotFound()
    {
        var periodId = Guid.CreateVersion7();
        _accounts.GetByPeriodAsync(periodId, Arg.Any<CancellationToken>()).Returns([]);

        var result = await _handler.Handle(new GetTrialBalanceQuery(periodId));

        var error = result.FirstErrorOrThrow();
        Assert.Equal(ErrorType.NotFound, error.Type);
        Assert.Equal(DomainErrors.FiscalPeriods.NotFound.Code, error.Code);
    }

    [Fact]
    public async Task Handle_WithPostedLines_ComputesPerAccountTotalsOrderedByNumber()
    {
        var periodId = Guid.CreateVersion7();
        var cash = TestDomain.NewAccount("1010", "Cash", Domain.Accounts.AccountType.Asset, periodId);
        var equity = TestDomain.NewAccount("3010", "Equity", Domain.Accounts.AccountType.Equity, periodId);
        var groceries = TestDomain.NewAccount("5010", "Groceries", Domain.Accounts.AccountType.Expense, periodId);
        _accounts.GetByPeriodAsync(periodId, Arg.Any<CancellationToken>()).Returns([groceries, cash, equity]);
        _entries.GetPostedLinesAsync(periodId, Arg.Any<CancellationToken>()).Returns(
        [
            (cash.Id, 500m, 0m),
            (equity.Id, 0m, 500m),
            (groceries.Id, 120m, 0m),
            (cash.Id, 0m, 120m)
        ]);

        var result = await _handler.Handle(new GetTrialBalanceQuery(periodId));

        Assert.False(result.IsError);
        var balance = result.Value;
        Assert.Equal(3, balance.Lines.Count);
        Assert.Equal(cash.Id, balance.Lines[0].AccountId);    // ordered by account number
        Assert.Equal(equity.Id, balance.Lines[1].AccountId);
        Assert.Equal(groceries.Id, balance.Lines[2].AccountId);
        Assert.Equal("1010", balance.Lines[0].AccountNumber);
        var cashLine = balance.Lines[0];
        Assert.Equal(500m, cashLine.TotalDebits);
        Assert.Equal(120m, cashLine.TotalCredits);
        Assert.Equal(380m, cashLine.Balance); // asset: debits - credits
        var equityLine = balance.Lines[1];
        Assert.Equal(500m, equityLine.Balance); // equity: credits - debits
        Assert.Equal(120m, balance.Lines[2].Balance); // expense: debits - credits
        Assert.Equal(620m, balance.TotalDebits);
        Assert.Equal(620m, balance.TotalCredits);
        Assert.True(balance.IsBalanced);
    }

    [Fact]
    public async Task Handle_WithoutPostedLines_ReturnsZeroTotals()
    {
        var periodId = Guid.CreateVersion7();
        _accounts.GetByPeriodAsync(periodId, Arg.Any<CancellationToken>()).Returns([TestDomain.NewAccount("1010", periodId: periodId)]);
        _entries.GetPostedLinesAsync(periodId, Arg.Any<CancellationToken>()).Returns([]);

        var result = await _handler.Handle(new GetTrialBalanceQuery(periodId));

        Assert.False(result.IsError);
        Assert.Equal(0m, result.Value.TotalDebits);
        Assert.Equal(0m, result.Value.TotalCredits);
        Assert.True(result.Value.IsBalanced);
        Assert.Equal(0m, Assert.Single(result.Value.Lines).Balance);
    }
}
