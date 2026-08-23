using LedgerLite.Domain.Accounts;
using LedgerLite.Domain.ValueObjects;

namespace LedgerLite.Domain.UnitTests.Accounts;

public sealed class AccountTests
{
    private static readonly Guid PeriodId = Guid.CreateVersion7();

    [Fact]
    public void TryCreate_WithValidInput_Succeeds()
    {
        var created = Account.TryCreate(AccountNumber.Create("1010"), "Cash", AccountType.Asset, PeriodId, out var account, out var error);

        Assert.True(created);
        Assert.Null(error);
        Assert.NotNull(account);
        Assert.Equal("1010", account!.Number.Value);
        Assert.Equal("Cash", account.Name);
        Assert.Equal(AccountType.Asset, account.Type);
        Assert.Equal(PeriodId, account.FiscalPeriodId);
        Assert.NotEqual(Guid.Empty, account.Id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryCreate_WithMissingName_Fails(string? name)
    {
        var created = Account.TryCreate(AccountNumber.Create("1010"), name, AccountType.Asset, PeriodId, out var account, out var error);

        Assert.False(created);
        Assert.Null(account);
        Assert.Contains("name is required", error);
    }

    [Fact]
    public void TryCreate_WithEmptyPeriodId_Fails()
    {
        var created = Account.TryCreate(AccountNumber.Create("1010"), "Cash", AccountType.Asset, Guid.Empty, out _, out var error);

        Assert.False(created);
        Assert.Contains("belong to a fiscal period", error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(99)]
    [InlineData(-1)]
    public void TryCreate_WithUndefinedAccountType_Fails(int typeValue)
    {
        var created = Account.TryCreate(
            AccountNumber.Create("1010"), "Cash", (AccountType)typeValue, PeriodId, out var account, out var error);

        Assert.False(created);
        Assert.Null(account);
        Assert.Contains("not valid", error);
    }

    [Fact]
    public void TryCreate_TrimsSurroundingWhitespaceFromName()
    {
        var created = Account.TryCreate(AccountNumber.Create("1010"), "  Cash Account  ", AccountType.Asset, PeriodId, out var account, out _);

        Assert.True(created);
        Assert.Equal("Cash Account", account!.Name);
    }
}
