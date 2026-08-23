using LedgerLite.Web.Client.Ui;

namespace LedgerLite.Web.Tests.Services;

public sealed class FormatTests
{
    [Theory]
    [InlineData(0d, "$0.00")]
    [InlineData(42d, "$42.00")]
    [InlineData(1234.56d, "$1,234.56")]
    [InlineData(-1234.56d, "-$1,234.56")]
    [InlineData(1234567.891d, "$1,234,567.89")]
    [InlineData(0.5d, "$0.50")]
    public void Money_formats_amounts(decimal amount, string expected)
    {
        Assert.Equal(expected, Format.Money(amount));
    }

    [Theory]
    [InlineData(0d, "0")]
    [InlineData(0.005d, "0.5")]
    [InlineData(0.5d, "50")]
    [InlineData(0.625d, "62.5")]
    [InlineData(1d, "100")]
    [InlineData(1.5d, "150")]
    [InlineData(0.999d, "99.9")]
    public void Percent_formats_ratio_without_unit(decimal ratio, string expected)
    {
        Assert.Equal(expected, Format.Percent(ratio));
    }

    [Fact]
    public void Date_renders_short_month_format()
    {
        Assert.Equal("Aug 22, 2026", Format.Date(new DateOnly(2026, 8, 22)));
    }

    [Fact]
    public void Timestamp_renders_utc_timestamp()
    {
        Assert.Equal("Aug 22, 2026 14:05", Format.Timestamp(new DateTime(2026, 8, 22, 14, 5, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void DateInput_renders_iso_date()
    {
        Assert.Equal("2026-08-22", Format.DateInput(new DateOnly(2026, 8, 22)));
    }
}
