using System.Diagnostics.CodeAnalysis;

namespace LedgerLite.Domain.ValueObjects;

/// <summary>A chart-of-accounts number in the range 1000-9999.</summary>
public readonly record struct AccountNumber
{
    public string Value { get; }

    private AccountNumber(string value) => Value = value;

    public static bool TryCreate(
        string? input,
        [NotNullWhen(true)] out AccountNumber accountNumber,
        [NotNullWhen(false)] out string? error)
    {
        var candidate = input?.Trim();
        if (string.IsNullOrWhiteSpace(candidate)
            || candidate.Length != 4
            || !candidate.All(char.IsDigit)
            || candidate[0] == '0')
        {
            accountNumber = default;
            error = "Account number must be a 4-digit string between '1000' and '9999'.";
            return false;
        }

        accountNumber = new AccountNumber(candidate);
        error = null;
        return true;
    }

    public static AccountNumber Create(string? input)
    {
        if (!TryCreate(input, out var number, out var error))
        {
            throw new ArgumentException(error, nameof(input));
        }

        return number;
    }

    public override string ToString() => Value;
}
