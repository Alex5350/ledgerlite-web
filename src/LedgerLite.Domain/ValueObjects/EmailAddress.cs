using System.Diagnostics.CodeAnalysis;

namespace LedgerLite.Domain.ValueObjects;

/// <summary>A validated email address value object (simple pragmatic validation, not RFC 5322).</summary>
public readonly record struct EmailAddress
{
    public string Value { get; }

    private EmailAddress(string value) => Value = value;

    public static bool TryCreate(
        string? input,
        [NotNullWhen(true)] out EmailAddress email,
        [NotNullWhen(false)] out string? error)
    {
        var candidate = input?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(candidate) || candidate.Length > 254)
        {
            email = default;
            error = "Email address is required and must be at most 254 characters.";
            return false;
        }

        if (!candidate.Contains('@') || candidate.StartsWith('@') || candidate.EndsWith('@'))
        {
            email = default;
            error = "Email address must contain a local part and a domain separated by '@'.";
            return false;
        }

        var parts = candidate.Split('@');
        if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || !parts[1].Contains('.'))
        {
            email = default;
            error = "Email address must have a valid domain (e.g. 'user@example.com').";
            return false;
        }

        email = new EmailAddress(candidate);
        error = null;
        return true;
    }

    public static EmailAddress Create(string? input)
    {
        if (!TryCreate(input, out var email, out var error))
        {
            throw new ArgumentException(error, nameof(input));
        }

        return email;
    }

    public override string ToString() => Value;
}
