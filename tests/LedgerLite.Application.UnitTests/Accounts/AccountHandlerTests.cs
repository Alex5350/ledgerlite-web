using ErrorOr;
using LedgerLite.Application.Abstractions;
using LedgerLite.Application.Common;
using LedgerLite.Application.Features.Accounts;
using LedgerLite.Domain.Accounts;
using LedgerLite.Domain.FiscalPeriods;
using LedgerLite.Domain.ValueObjects;

namespace LedgerLite.Application.UnitTests.Accounts;

public sealed class CreateAccountHandlerTests
{
    private readonly IAccountRepository _accounts = Substitute.For<IAccountRepository>();
    private readonly IFiscalPeriodRepository _periods = Substitute.For<IFiscalPeriodRepository>();
    private readonly CreateAccountHandler _handler;

    public CreateAccountHandlerTests()
    {
        _handler = new CreateAccountHandler(_accounts, _periods, new CreateAccountValidator());
    }

    private CreateAccountCommand ValidCommand(Guid? periodId = null, string number = "1010") =>
        new(number, "Cash", AccountType.Asset, periodId ?? Guid.CreateVersion7());

    [Fact]
    public async Task Handle_WithValidCommand_AddsAccount()
    {
        var periodId = Guid.CreateVersion7();
        _periods.GetByIdAsync(periodId, Arg.Any<CancellationToken>()).Returns(TestDomain.OpenPeriod());
        _accounts.NumberExistsInPeriodAsync(Arg.Any<AccountNumber>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(false);
        Account? added = null;
        _ = _accounts.AddAsync(Arg.Do<Account>(account => added = account), Arg.Any<CancellationToken>());

        var result = await _handler.Handle(ValidCommand(periodId));

        Assert.False(result.IsError);
        Assert.NotNull(added);
        Assert.Equal(added!.Id, result.Value.Id);
        Assert.Equal("1010", added.Number.Value);
        Assert.Equal("Cash", added.Name);
        Assert.Equal(AccountType.Asset, added.Type);
        Assert.Equal(periodId, added.FiscalPeriodId);
        await _accounts.Received(1).AddAsync(Arg.Any<Account>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_WithMissingName_ReturnsValidationError(string? name)
    {
        var command = new CreateAccountCommand("1010", name!, AccountType.Asset, Guid.CreateVersion7());

        var result = await _handler.Handle(command);

        var error = result.FirstErrorOrThrow();
        Assert.Equal(ErrorType.Validation, error.Type);
        Assert.Equal("Name", error.Code);
    }

    [Fact]
    public async Task Handle_WithEmptyPeriodId_ReturnsValidationError()
    {
        var result = await _handler.Handle(ValidCommand(periodId: Guid.Empty));

        Assert.Equal("PeriodId", result.FirstErrorOrThrow().Code);
    }

    [Theory]
    [InlineData("0999")]
    [InlineData("123")]
    [InlineData("12a4")]
    [InlineData("10000")]
    public async Task Handle_WithInvalidAccountNumber_ReturnsInvalidNumberError(string number)
    {
        var result = await _handler.Handle(ValidCommand(number: number));

        var error = result.FirstErrorOrThrow();
        Assert.Equal(ErrorType.Validation, error.Type);
        Assert.Equal(DomainErrors.Accounts.InvalidNumber.Code, error.Code);
        await _accounts.DidNotReceive().AddAsync(Arg.Any<Account>(), Arg.Any<CancellationToken>());
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
    public async Task Handle_WhenNumberAlreadyTakenInPeriod_ReturnsConflict()
    {
        var periodId = Guid.CreateVersion7();
        _periods.GetByIdAsync(periodId, Arg.Any<CancellationToken>()).Returns(TestDomain.OpenPeriod());
        _accounts.NumberExistsInPeriodAsync(Arg.Any<AccountNumber>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(true);

        var result = await _handler.Handle(ValidCommand(periodId));

        var error = result.FirstErrorOrThrow();
        Assert.Equal(ErrorType.Conflict, error.Type);
        Assert.Equal(DomainErrors.Accounts.NumberTaken.Code, error.Code);
        await _accounts.DidNotReceive().AddAsync(Arg.Any<Account>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithUndefinedAccountType_ReturnsValidationError()
    {
        var periodId = Guid.CreateVersion7();
        _periods.GetByIdAsync(periodId, Arg.Any<CancellationToken>()).Returns(TestDomain.OpenPeriod());
        _accounts.NumberExistsInPeriodAsync(Arg.Any<AccountNumber>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(false);
        var command = new CreateAccountCommand("1010", "Cash", (AccountType)99, periodId);

        var result = await _handler.Handle(command);

        var error = result.FirstErrorOrThrow();
        Assert.Equal(ErrorType.Validation, error.Type);
        Assert.Equal("Accounts.Invalid", error.Code);
        Assert.Contains("not valid", error.Description);
    }
}

public sealed class GetAccountByIdHandlerTests
{
    private readonly IAccountRepository _accounts = Substitute.For<IAccountRepository>();
    private readonly GetAccountByIdHandler _handler;

    public GetAccountByIdHandlerTests()
    {
        _handler = new GetAccountByIdHandler(_accounts);
    }

    [Fact]
    public async Task Handle_WhenAccountExists_MapsToDto()
    {
        var account = TestDomain.NewAccount(number: "5010", name: "Groceries", type: AccountType.Expense);
        _accounts.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);

        var result = await _handler.Handle(new GetAccountByIdQuery(account.Id));

        Assert.False(result.IsError);
        var dto = result.Value;
        Assert.Equal(account.Id, dto.Id);
        Assert.Equal("5010", dto.Number);
        Assert.Equal("Groceries", dto.Name);
        Assert.Equal("Expense", dto.Type);
        Assert.Equal(account.FiscalPeriodId, dto.FiscalPeriodId);
    }

    [Fact]
    public async Task Handle_WhenAccountIsMissing_ReturnsNotFound()
    {
        _accounts.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Account?)null);

        var result = await _handler.Handle(new GetAccountByIdQuery(Guid.CreateVersion7()));

        var error = result.FirstErrorOrThrow();
        Assert.Equal(ErrorType.NotFound, error.Type);
        Assert.Equal(DomainErrors.Accounts.NotFound.Code, error.Code);
    }
}
