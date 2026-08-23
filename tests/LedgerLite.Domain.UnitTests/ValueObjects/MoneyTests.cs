using System.Globalization;
using LedgerLite.Domain.ValueObjects;

namespace LedgerLite.Domain.UnitTests.ValueObjects;

public sealed class MoneyTests
{
    [Fact]
    public void TryCreate_WithValidAmountAndCurrency_Succeeds()
    {
        var created = Money.TryCreate(12.34m, "usd", out var money, out var error);

        Assert.True(created);
        Assert.Null(error);
        Assert.Equal(12.34m, money.Amount);
        Assert.Equal("USD", money.Currency);
    }

    [Fact]
    public void TryCreate_WithZeroAmount_Succeeds()
    {
        var created = Money.TryCreate(0m, "USD", out var money, out _);

        Assert.True(created);
        Assert.True(money.IsZero);
        Assert.False(money.IsPositive);
    }

    [Theory]
    [InlineData("-0.01")]
    [InlineData("-1000")]
    public void TryCreate_WhenAmountIsNegative_Fails(string amount)
    {
        var created = Money.TryCreate(decimal.Parse(amount, CultureInfo.InvariantCulture), "USD", out var money, out var error);

        Assert.False(created);
        Assert.Equal(default, money);
        Assert.Contains("negative", error);
    }

    [Theory]
    [InlineData("1.234")]
    [InlineData("0.001")]
    [InlineData("999.999")]
    public void TryCreate_WhenAmountHasMoreThanTwoDecimalPlaces_Fails(string amount)
    {
        var created = Money.TryCreate(decimal.Parse(amount, CultureInfo.InvariantCulture), "USD", out _, out var error);

        Assert.False(created);
        Assert.Contains("two decimal places", error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("US")]
    [InlineData("USDD")]
    [InlineData("U5D")]
    [InlineData("123")]
    public void TryCreate_WhenCurrencyIsInvalid_Fails(string? currency)
    {
        var created = Money.TryCreate(5m, currency, out var money, out var error);

        Assert.False(created);
        Assert.Equal(default, money);
        Assert.Contains("ISO 4217", error);
    }

    [Theory]
    [InlineData("usd", "USD")]
    [InlineData("  Eur ", "EUR")]
    [InlineData("GBP", "GBP")]
    public void TryCreate_NormalizesCurrencyToUppercase(string currency, string expected)
    {
        var created = Money.TryCreate(5m, currency, out var money, out _);

        Assert.True(created);
        Assert.Equal(expected, money.Currency);
    }

    [Fact]
    public void Create_WithValidInput_ReturnsMoney()
    {
        var money = Money.Create(5m, "USD");

        Assert.Equal(5m, money.Amount);
        Assert.Equal("USD", money.Currency);
    }

    [Fact]
    public void Create_WithoutCurrency_ThrowsBecauseCurrencyIsRequired()
    {
        // The currency parameter has no usable default: 'null' fails ISO 4217 validation.
        Assert.Throws<ArgumentException>(() => Money.Create(5m));
    }

    [Theory]
    [InlineData("-5", "USD")]
    [InlineData("1.234", "USD")]
    [InlineData("5", "US")]
    [InlineData("5", null)]
    public void Create_WithInvalidInput_ThrowsArgumentException(string amount, string? currency)
    {
        Assert.Throws<ArgumentException>(
            () => Money.Create(decimal.Parse(amount, CultureInfo.InvariantCulture), currency));
    }

    [Fact]
    public void Add_WithSameCurrency_SumsAmounts()
    {
        var left = Money.Create(1.50m, "USD");
        var right = Money.Create(2.25m, "USD");

        var sum = left.Add(right);

        Assert.Equal(3.75m, sum.Amount);
        Assert.Equal("USD", sum.Currency);
    }

    [Fact]
    public void Add_WithDifferentCurrencies_Throws()
    {
        var usd = Money.Create(1m, "USD");
        var eur = Money.Create(1m, "EUR");

        var ex = Record.Exception(() => usd.Add(eur));

        Assert.IsType<InvalidOperationException>(ex);
        Assert.Contains("different currencies", ex.Message);
    }

    [Fact]
    public void Subtract_WithSameCurrency_SubtractsAmounts()
    {
        var left = Money.Create(5m, "USD");
        var right = Money.Create(2m, "USD");

        var difference = left.Subtract(right);

        Assert.Equal(3m, difference.Amount);
    }

    [Fact]
    public void Subtract_WhenResultWouldBeNegative_Throws()
    {
        var left = Money.Create(2m, "USD");
        var right = Money.Create(5m, "USD");

        var ex = Record.Exception(() => left.Subtract(right));

        Assert.IsType<InvalidOperationException>(ex);
        Assert.Contains("negative", ex.Message);
    }

    [Fact]
    public void Subtract_WithDifferentCurrencies_Throws()
    {
        var usd = Money.Create(5m, "USD");
        var eur = Money.Create(1m, "EUR");

        Assert.Throws<InvalidOperationException>(() => usd.Subtract(eur));
    }

    [Fact]
    public void Zero_WithExplicitCurrency_UsesThatCurrency()
    {
        var zero = Money.Zero("EUR");

        Assert.Equal(0m, zero.Amount);
        Assert.Equal("EUR", zero.Currency);
        Assert.True(zero.IsZero);
    }

    [Fact]
    public void Equality_IgnoresCurrencyCaseAndMatchesOnAmount()
    {
        var left = Money.Create(5m, "USD");
        var right = Money.Create(5m, "usd");

        Assert.True(left == right);
        Assert.Equal(left, right);

        Assert.NotEqual(left, Money.Create(6m, "USD"));
        Assert.NotEqual(left, Money.Create(5m, "EUR"));
    }

    [Theory]
    [InlineData("1.2", false)]
    [InlineData("1.23", false)]
    [InlineData("1.230", false)]
    [InlineData("100", false)]
    [InlineData("0", false)]
    [InlineData("1.234", true)]
    [InlineData("0.001", true)]
    [InlineData("999999999.999", true)]
    public void HasMoreThanTwoDecimalPlaces_DetectsSubCentPrecision(string value, bool expected)
    {
        var amount = decimal.Parse(value, CultureInfo.InvariantCulture);

        Assert.Equal(expected, amount.HasMoreThanTwoDecimalPlaces);
    }

    [Fact]
    public void ToString_RendersAmountAndCurrency()
    {
        Assert.Equal("12.35 USD", Money.Create(12.35m, "USD").ToString());
    }
}
