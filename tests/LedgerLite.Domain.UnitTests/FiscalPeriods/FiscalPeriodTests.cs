using LedgerLite.Domain.FiscalPeriods;

namespace LedgerLite.Domain.UnitTests.FiscalPeriods;

public sealed class FiscalPeriodTests
{
    private static readonly DateOnly Start = new(2026, 1, 1);
    private static readonly DateOnly End = new(2026, 1, 31);

    public sealed class Create
    {
        [Fact]
        public void WithValidRange_SucceedsAndStartsOpen()
        {
            var created = FiscalPeriod.TryCreate("January 2026", Start, End, out var period, out var error);

            Assert.True(created);
            Assert.Null(error);
            Assert.NotNull(period);
            Assert.Equal("January 2026", period!.Name);
            Assert.Equal(Start, period.StartDate);
            Assert.Equal(End, period.EndDate);
            Assert.Equal(FiscalPeriodStatus.Open, period.Status);
            Assert.True(period.IsOpen);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void WithMissingName_Fails(string? name)
        {
            var created = FiscalPeriod.TryCreate(name, Start, End, out var period, out var error);

            Assert.False(created);
            Assert.Null(period);
            Assert.Contains("name is required", error);
        }

        [Fact]
        public void WithEndDateBeforeStartDate_Fails()
        {
            var created = FiscalPeriod.TryCreate("Q1", End, Start, out var period, out var error);

            Assert.False(created);
            Assert.Null(period);
            Assert.Contains("end date must not be before", error);
        }

        [Fact]
        public void WithSingleDayRange_Succeeds()
        {
            var created = FiscalPeriod.TryCreate("Single day", Start, Start, out var period, out _);

            Assert.True(created);
            Assert.Equal(Start, period!.StartDate);
            Assert.Equal(Start, period.EndDate);
        }

        [Fact]
        public void TrimsSurroundingWhitespaceFromName()
        {
            var created = FiscalPeriod.TryCreate("  February 2026  ", Start, End, out var period, out _);

            Assert.True(created);
            Assert.Equal("February 2026", period!.Name);
        }
    }

    public sealed class Close
    {
        [Fact]
        public void BeforeEndDate_ReturnsErrorAndStaysOpen()
        {
            if (!FiscalPeriod.TryCreate("January 2026", Start, End, out var period, out var error))
            {
                Assert.Fail(error);
            }

            var closed = period!.TryClose(End.AddDays(-1), out var closeError);

            Assert.False(closed);
            Assert.Contains("cannot be closed before", closeError);
            Assert.Equal(FiscalPeriodStatus.Open, period.Status);
            Assert.True(period.IsOpen);
        }

        [Fact]
        public void OnEndDate_Succeeds()
        {
            if (!FiscalPeriod.TryCreate("January 2026", Start, End, out var period, out _))
            {
                Assert.Fail("Period should be created");
            }

            var closed = period!.TryClose(End, out var error);

            Assert.True(closed);
            Assert.Null(error);
        }

        [Fact]
        public void AfterEndDate_SucceedsAndTransitionsToClosed()
        {
            if (!FiscalPeriod.TryCreate("January 2026", Start, End, out var period, out _))
            {
                Assert.Fail("Period should be created");
            }

            var closed = period!.TryClose(End.AddDays(1), out var error);

            Assert.True(closed);
            Assert.Null(error);
            Assert.Equal(FiscalPeriodStatus.Closed, period.Status);
            Assert.False(period.IsOpen);
        }

        [Fact]
        public void WhenAlreadyClosed_ReturnsError()
        {
            if (!FiscalPeriod.TryCreate("January 2026", Start, End, out var period, out _))
            {
                Assert.Fail("Period should be created");
            }

            Assert.True(period!.TryClose(End, out _));

            var closedAgain = period.TryClose(End.AddDays(1), out var error);

            Assert.False(closedAgain);
            Assert.Contains("already closed", error);
            Assert.Equal(FiscalPeriodStatus.Closed, period.Status);
        }
    }
}
