using ErrorOr;
using LedgerLite.Application.Abstractions;
using LedgerLite.Application.Common;
using LedgerLite.Application.Features.FiscalPeriods;
using LedgerLite.Domain.FiscalPeriods;

namespace LedgerLite.Application.UnitTests.FiscalPeriods;

public sealed class CreateFiscalPeriodHandlerTests
{
    private readonly IFiscalPeriodRepository _periods = Substitute.For<IFiscalPeriodRepository>();
    private readonly CreateFiscalPeriodHandler _handler;

    public CreateFiscalPeriodHandlerTests()
    {
        _handler = new CreateFiscalPeriodHandler(_periods, new CreateFiscalPeriodValidator());
    }

    private static CreateFiscalPeriodCommand ValidCommand() =>
        new("September 2026", new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30));

    [Fact]
    public async Task Handle_WithValidCommand_AddsOpenPeriod()
    {
        FiscalPeriod? added = null;
        _ = _periods.AddAsync(Arg.Do<FiscalPeriod>(period => added = period), Arg.Any<CancellationToken>());

        var result = await _handler.Handle(ValidCommand());

        Assert.False(result.IsError);
        Assert.NotNull(added);
        Assert.Equal(added!.Id, result.Value.Id);
        Assert.Equal("September 2026", added.Name);
        Assert.Equal(new DateOnly(2026, 9, 1), added.StartDate);
        Assert.Equal(new DateOnly(2026, 9, 30), added.EndDate);
        Assert.Equal(FiscalPeriodStatus.Open, added.Status);
        await _periods.Received(1).AddAsync(Arg.Any<FiscalPeriod>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_TrimsPeriodName()
    {
        FiscalPeriod? added = null;
        _ = _periods.AddAsync(Arg.Do<FiscalPeriod>(period => added = period), Arg.Any<CancellationToken>());

        var command = ValidCommand() with { Name = "  September 2026  " };
        await _handler.Handle(command);

        Assert.Equal("September 2026", added!.Name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_WithMissingName_ReturnsValidationError(string? name)
    {
        var command = new CreateFiscalPeriodCommand(name!, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30));

        var result = await _handler.Handle(command);

        var error = result.FirstErrorOrThrow();
        Assert.Equal(ErrorType.Validation, error.Type);
        Assert.Equal("Name", error.Code);
        await _periods.DidNotReceive().AddAsync(Arg.Any<FiscalPeriod>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithEndDateBeforeStartDate_ReturnsValidationError()
    {
        var command = new CreateFiscalPeriodCommand("Q1", new DateOnly(2026, 3, 31), new DateOnly(2026, 1, 1));

        var result = await _handler.Handle(command);

        var error = result.FirstErrorOrThrow();
        Assert.Equal(ErrorType.Validation, error.Type);
        Assert.Equal("EndDate", error.Code);
        Assert.Contains("end date must not be before", error.Description);
    }

    [Fact]
    public async Task Handle_WithOverlyLongName_ReturnsValidationError()
    {
        var command = ValidCommand() with { Name = new string('a', 101) };

        var result = await _handler.Handle(command);

        Assert.Equal("Name", result.FirstErrorOrThrow().Code);
    }
}

public sealed class CloseFiscalPeriodHandlerTests
{
    private readonly IFiscalPeriodRepository _periods = Substitute.For<IFiscalPeriodRepository>();
    private readonly CloseFiscalPeriodHandler _handler;

    public CloseFiscalPeriodHandlerTests()
    {
        _handler = new CloseFiscalPeriodHandler(_periods);
    }

    [Fact]
    public async Task Handle_WhenPeriodIsMissing_ReturnsNotFound()
    {
        _periods.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((FiscalPeriod?)null);
        var command = new CloseFiscalPeriodCommand(Guid.CreateVersion7());

        var result = await _handler.Handle(command);

        var error = result.FirstErrorOrThrow();
        Assert.Equal(ErrorType.NotFound, error.Type);
        Assert.Equal(DomainErrors.FiscalPeriods.NotFound.Code, error.Code);
    }

    [Fact]
    public async Task Handle_WhenEndDateHasPassed_ClosesPeriodAndSucceeds()
    {
        var period = TestDomain.OpenPeriod(start: new DateOnly(2026, 1, 1), end: new DateOnly(2026, 1, 31));
        _periods.GetByIdAsync(period.Id, Arg.Any<CancellationToken>()).Returns(period);

        var result = await _handler.Handle(new CloseFiscalPeriodCommand(period.Id));

        Assert.False(result.IsError);
        Assert.Equal(FiscalPeriodStatus.Closed, period.Status);
    }

    [Fact]
    public async Task Handle_WhenEndDateIsInTheFuture_ReturnsConflict()
    {
        var future = TestDomain.OpenPeriod(start: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)), end: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(60)));
        _periods.GetByIdAsync(future.Id, Arg.Any<CancellationToken>()).Returns(future);

        var result = await _handler.Handle(new CloseFiscalPeriodCommand(future.Id));

        var error = result.FirstErrorOrThrow();
        Assert.Equal(ErrorType.Conflict, error.Type);
        Assert.Equal("FiscalPeriods.CannotClose", error.Code);
        Assert.Equal(FiscalPeriodStatus.Open, future.Status);
    }

    [Fact]
    public async Task Handle_WhenAlreadyClosed_ReturnsConflict()
    {
        var closed = TestDomain.ClosedPeriod();
        _periods.GetByIdAsync(closed.Id, Arg.Any<CancellationToken>()).Returns(closed);

        var result = await _handler.Handle(new CloseFiscalPeriodCommand(closed.Id));

        var error = result.FirstErrorOrThrow();
        Assert.Equal(ErrorType.Conflict, error.Type);
        Assert.Equal("FiscalPeriods.CannotClose", error.Code);
    }
}
