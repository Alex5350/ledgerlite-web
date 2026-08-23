using LedgerLite.Domain.ValueObjects;

namespace LedgerLite.Domain.UnitTests.ValueObjects;

public sealed class AccountNumberTests
{
    [Theory]
    [InlineData("1000")]
    [InlineData("1234")]
    [InlineData("9999")]
    [InlineData(" 1234 ")]
    [InlineData("\t5678\n")]
    public void TryCreate_WithFourDigitNumberBetween1000And9999_Succeeds(string input)
    {
        var created = AccountNumber.TryCreate(input, out var number, out var error);

        Assert.True(created);
        Assert.Null(error);
        Assert.Equal(input.Trim(), number.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("999")]      // too short
    [InlineData("10000")]    // too long
    [InlineData("12a4")]     // not all digits
    [InlineData("0123")]     // leading zero -> below 1000
    [InlineData("0999")]     // leading zero
    [InlineData("12 4")]     // embedded space
    public void TryCreate_WithInvalidInput_Fails(string? input)
    {
        var created = AccountNumber.TryCreate(input, out var number, out var error);

        Assert.False(created);
        Assert.Equal(default, number);
        Assert.Contains("between '1000' and '9999'", error);
    }

    [Fact]
    public void Create_WithValidInput_ReturnsNumber()
    {
        var number = AccountNumber.Create(" 4242 ");

        Assert.Equal("4242", number.Value);
    }

    [Fact]
    public void Create_WithInvalidInput_Throws()
    {
        Assert.Throws<ArgumentException>(() => AccountNumber.Create("0123"));
    }

    [Fact]
    public void Equality_IsBasedOnValue()
    {
        Assert.True(AccountNumber.Create("1234") == AccountNumber.Create("1234"));
        Assert.False(AccountNumber.Create("1234").Equals(AccountNumber.Create("5678")));
    }

    [Fact]
    public void ToString_ReturnsRawValue()
    {
        Assert.Equal("8642", AccountNumber.Create("8642").ToString());
    }
}
