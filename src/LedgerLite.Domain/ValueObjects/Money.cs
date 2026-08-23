using System.Diagnostics.CodeAnalysis;

namespace LedgerLite.Domain.ValueObjects;

/// <summary>
/// An amount paired with a currency (ISO 4217 alpha-3 code).
/// Immutable value object with checked arithmetic.
/// </summary>
public readonly record struct Money
{
    public const string DefaultCurrency = "USD";

    public decimal Amount { get; }
    public string Currency { get; }

    private Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }

    public static Money Zero(string currency = DefaultCurrency) => new(0m, currency);

    public static bool TryCreate(
        decimal amount,
        string? currency,
        [NotNullWhen(true)] out Money money,
        [NotNullWhen(false)] out string? error)
    {
        if (amount < 0)
        {
            money = default;
            error = "Money amount cannot be negative.";
            return false;
        }

        if (amount > 79228162514264337593543950335m)
        {
            money = default;
            error = "Money amount is too large.";
            return false;
        }

        if (amount.HasMoreThanTwoDecimalPlaces)
        {
            money = default;
            error = "Money amount cannot have more than two decimal places.";
            return false;
        }

        var normalized = currency?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length != 3 || !normalized.All(char.IsLetter))
        {
            money = default;
            error = "Currency must be a 3-letter ISO 4217 alpha code (e.g. 'USD').";
            return false;
        }

        money = new Money(amount, normalized);
        error = null;
        return true;
    }

    public static Money Create(decimal amount, string? currency = null)
    {
        if (!TryCreate(amount, currency, out var money, out var error))
        {
            throw new ArgumentException(error, nameof(amount));
        }

        return money;
    }

    public Money Add(Money other) => Apply(other, static (a, b) => a + b, "add");

    public Money Subtract(Money other) => Apply(other, static (a, b) => a - b, "subtract");

    private Money Apply(Money other, Func<decimal, decimal, decimal> operation, string operationName)
    {
        if (Currency != other.Currency)
        {
            throw new InvalidOperationException(
                $"Cannot {operationName} amounts in different currencies ({Currency} vs {other.Currency}).");
        }

        var result = operation(Amount, other.Amount);
        if (result < 0)
        {
            throw new InvalidOperationException(
                $"Cannot {operationName} {other.Amount} {Currency} from {Amount} {Currency}: result would be negative.");
        }

        return new Money(result, Currency);
    }

    public bool IsZero => Amount == 0m;

    public bool IsPositive => Amount > 0m;

    public override string ToString() => $"{Amount:0.##} {Currency}";
}

public static class DecimalPrecisionExtensions
{
    extension(decimal value)
    {
        internal bool HasMoreThanTwoDecimalPlaces => value != Math.Round(value, 2);
    }
}
