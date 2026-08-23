using LedgerLite.Domain.Budgets;
using LedgerLite.Domain.Events;
using LedgerLite.Domain.ValueObjects;

namespace LedgerLite.Domain.UnitTests.Budgets;

public sealed class BudgetTests
{
    private static readonly Guid PeriodId = Guid.CreateVersion7();

    private static Money Usd(decimal amount) => Money.Create(amount, "USD");

    private static Budget CreateBudget(decimal limitAmount = 100m) =>
        Budget.TryCreate(PeriodId, "Groceries", Usd(limitAmount), out var budget, out var error)
            ? budget!
            : throw new InvalidOperationException(error);

    public sealed class Create
    {
        [Fact]
        public void WithValidInput_Succeeds()
        {
            var created = Budget.TryCreate(PeriodId, "Groceries", Usd(500m), out var budget, out var error);

            Assert.True(created);
            Assert.Null(error);
            Assert.NotNull(budget);
            Assert.Equal(PeriodId, budget!.FiscalPeriodId);
            Assert.Equal("Groceries", budget.Category);
            Assert.Equal(500m, budget.Limit.Amount);
            Assert.Equal("USD", budget.Limit.Currency);
            Assert.Equal(BudgetThreshold.None, budget.NotifiedThresholds);
        }

        [Fact]
        public void WithEmptyPeriodId_Fails()
        {
            var created = Budget.TryCreate(Guid.Empty, "Groceries", Usd(500m), out var budget, out var error);

            Assert.False(created);
            Assert.Null(budget);
            Assert.Contains("fiscal period", error);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void WithMissingCategory_Fails(string? category)
        {
            var created = Budget.TryCreate(PeriodId, category, Usd(500m), out var budget, out var error);

            Assert.False(created);
            Assert.Contains("category is required", error);
        }

        [Fact]
        public void WithZeroLimit_Fails()
        {
            var created = Budget.TryCreate(PeriodId, "Groceries", Money.Zero(), out var budget, out var error);

            Assert.False(created);
            Assert.Contains("greater than zero", error);
        }

        [Fact]
        public void TrimsSurroundingWhitespaceFromCategory()
        {
            var created = Budget.TryCreate(PeriodId, "  Travel  ", Usd(500m), out var budget, out _);

            Assert.True(created);
            Assert.Equal("Travel", budget!.Category);
        }
    }

    public sealed class EvaluateSpending
    {
        [Fact]
        public void WithMismatchedCurrency_Throws()
        {
            var budget = CreateBudget();
            var spent = Money.Create(10m, "EUR");

            Assert.Throws<InvalidOperationException>(() => budget.EvaluateSpending(spent));
        }

        [Fact]
        public void BelowEightyPercent_DoesNotRaiseEvent()
        {
            var budget = CreateBudget(limitAmount: 100m);

            budget.EvaluateSpending(Usd(79m));

            Assert.Empty(budget.DomainEvents);
            Assert.Equal(BudgetThreshold.None, budget.NotifiedThresholds);
        }

        [Fact]
        public void AtExactlyEightyPercent_RaisesSingleEvent()
        {
            var budget = CreateBudget(limitAmount: 100m);

            budget.EvaluateSpending(Usd(80m));

            var raised = Assert.IsType<BudgetThresholdExceededDomainEvent>(Assert.Single(budget.DomainEvents));
            Assert.Equal(BudgetThreshold.EightyPercent, raised.Threshold);
            Assert.Equal(BudgetThreshold.EightyPercent, budget.NotifiedThresholds);
            Assert.Equal(budget.Id, raised.BudgetId);
            Assert.Equal(PeriodId, raised.FiscalPeriodId);
            Assert.Equal("Groceries", raised.Category);
            Assert.Equal(100m, raised.Limit.Amount);
            Assert.Equal(80m, raised.Spent.Amount);
        }

        [Fact]
        public void ReEvaluationAtSameLevel_DoesNotRaiseDuplicateEvent()
        {
            var budget = CreateBudget(limitAmount: 100m);
            budget.EvaluateSpending(Usd(80m));
            Assert.Single(budget.DomainEvents);

            budget.EvaluateSpending(Usd(85m));

            Assert.Single(budget.DomainEvents);
            Assert.Equal(BudgetThreshold.EightyPercent, budget.NotifiedThresholds);
        }

        [Fact]
        public void CrossingHundredPercentAfterEighty_RaisesSecondEvent()
        {
            var budget = CreateBudget(limitAmount: 100m);
            budget.EvaluateSpending(Usd(80m));

            budget.EvaluateSpending(Usd(100m));

            Assert.Equal(2, budget.DomainEvents.Count);
            var latest = Assert.IsType<BudgetThresholdExceededDomainEvent>(budget.DomainEvents[^1]);
            Assert.Equal(BudgetThreshold.HundredPercent, latest.Threshold);
            Assert.True(budget.NotifiedThresholds.HasFlag(BudgetThreshold.EightyPercent));
            Assert.True(budget.NotifiedThresholds.HasFlag(BudgetThreshold.HundredPercent));
        }

        [Fact]
        public void DirectlyAtHundredPercent_RaisesOnlyHundredPercentEvent()
        {
            var budget = CreateBudget(limitAmount: 100m);

            budget.EvaluateSpending(Usd(150m));

            var raised = Assert.IsType<BudgetThresholdExceededDomainEvent>(Assert.Single(budget.DomainEvents));
            Assert.Equal(BudgetThreshold.HundredPercent, raised.Threshold);
            Assert.True(budget.NotifiedThresholds.HasFlag(BudgetThreshold.EightyPercent));
            Assert.True(budget.NotifiedThresholds.HasFlag(BudgetThreshold.HundredPercent));
        }

        [Fact]
        public void ReEvaluationAtHundredPercent_DoesNotRaiseDuplicateEvent()
        {
            var budget = CreateBudget(limitAmount: 100m);
            budget.EvaluateSpending(Usd(120m));
            Assert.Single(budget.DomainEvents);

            budget.EvaluateSpending(Usd(130m));

            Assert.Single(budget.DomainEvents);
        }

        [Fact]
        public void WithZeroSpending_DoesNotRaiseEvent()
        {
            var budget = CreateBudget(limitAmount: 100m);

            budget.EvaluateSpending(Usd(0m));

            Assert.Empty(budget.DomainEvents);
            Assert.Equal(BudgetThreshold.None, budget.NotifiedThresholds);
        }
    }
}
