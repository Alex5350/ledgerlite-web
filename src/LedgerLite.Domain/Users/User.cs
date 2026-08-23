using LedgerLite.Domain.Common;
using LedgerLite.Domain.ValueObjects;

namespace LedgerLite.Domain.Users;

/// <summary>Identity member aggregate.</summary>
public sealed class User : Entity
{
    private User(EmailAddress email, string displayName, string passwordHash)
    {
        Email = email;
        DisplayName = displayName;
        PasswordHash = passwordHash;
    }

    public EmailAddress Email { get; }

    // C# 14 'field' keyword: normalize on assignment without a manual backing field.
    public string DisplayName
    {
        get => field;
        private set => field = value.Trim();
    } = string.Empty;

    public string PasswordHash { get; }

    public static User Create(EmailAddress email, string displayName, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("Display name is required.", nameof(displayName));
        }

        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new ArgumentException("Password hash is required.", nameof(passwordHash));
        }

        return new User(email, displayName, passwordHash);
    }
}
