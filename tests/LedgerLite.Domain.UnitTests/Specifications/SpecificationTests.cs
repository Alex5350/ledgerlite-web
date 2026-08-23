using System.Linq.Expressions;
using LedgerLite.Domain.Accounts;
using LedgerLite.Domain.Specifications;
using LedgerLite.Domain.ValueObjects;

namespace LedgerLite.Domain.UnitTests.Specifications;

public sealed class SpecificationTests
{
    private static readonly Guid FirstPeriodId = Guid.CreateVersion7();
    private static readonly Guid SecondPeriodId = Guid.CreateVersion7();

    private static Account NewAccount(AccountType type, Guid? fiscalPeriodId = null, string number = "1234") =>
        Account.TryCreate(AccountNumber.Create(number), "Test account", type, fiscalPeriodId ?? FirstPeriodId, out var account, out var error)
            ? account!
            : throw new InvalidOperationException(error);

    [Theory]
    [InlineData(AccountType.Asset, true)]
    [InlineData(AccountType.Liability, false)]
    [InlineData(AccountType.Expense, false)]
    public void AccountTypeSpecification_MatchesOnlyTheRequestedType(AccountType type, bool expected)
    {
        var specification = new AccountTypeSpecification(AccountType.Asset);
        var candidate = NewAccount(type);

        Assert.Equal(expected, specification.IsSatisfiedBy(candidate));
    }

    [Fact]
    public void AccountTypeSpecification_WithPeriodFilter_MatchesOnlyThatPeriod()
    {
        var specification = new AccountTypeSpecification(AccountType.Asset, FirstPeriodId);

        Assert.True(specification.IsSatisfiedBy(NewAccount(AccountType.Asset, FirstPeriodId)));
        Assert.False(specification.IsSatisfiedBy(NewAccount(AccountType.Asset, SecondPeriodId)));
        Assert.False(specification.IsSatisfiedBy(NewAccount(AccountType.Expense, FirstPeriodId)));
    }

    [Fact]
    public void And_RequiresBothSides()
    {
        var expensesInPeriod = new AccountTypeSpecification(AccountType.Expense, FirstPeriodId);
        var expenseOrLiability = new AccountTypeSpecification(AccountType.Liability)
            .Or(new AccountTypeSpecification(AccountType.Expense));
        var combined = expensesInPeriod.And(expenseOrLiability);

        Assert.True(combined.IsSatisfiedBy(NewAccount(AccountType.Expense, FirstPeriodId)));
        Assert.False(combined.IsSatisfiedBy(NewAccount(AccountType.Liability, FirstPeriodId)));
        Assert.False(combined.IsSatisfiedBy(NewAccount(AccountType.Expense, SecondPeriodId)));
        Assert.False(combined.IsSatisfiedBy(NewAccount(AccountType.Asset, FirstPeriodId)));
    }

    [Fact]
    public void Or_RequiresEitherSide()
    {
        var assets = new AccountTypeSpecification(AccountType.Asset);
        var liabilities = new AccountTypeSpecification(AccountType.Liability);
        var assetsOrLiabilities = assets.Or(liabilities);

        Assert.True(assetsOrLiabilities.IsSatisfiedBy(NewAccount(AccountType.Asset)));
        Assert.True(assetsOrLiabilities.IsSatisfiedBy(NewAccount(AccountType.Liability)));
        Assert.False(assetsOrLiabilities.IsSatisfiedBy(NewAccount(AccountType.Expense)));
        Assert.False(assetsOrLiabilities.IsSatisfiedBy(NewAccount(AccountType.Revenue)));
    }

    [Fact]
    public void ToExpression_CanBeCompiledForExternalUse()
    {
        Expression<Func<Account, bool>> expression = new AccountTypeSpecification(AccountType.Expense).ToExpression();
        var compiled = expression.Compile();

        Assert.True(compiled(NewAccount(AccountType.Expense)));
        Assert.False(compiled(NewAccount(AccountType.Revenue)));
    }
}
