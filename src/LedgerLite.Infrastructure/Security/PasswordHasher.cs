using LedgerLite.Application.Abstractions;

using IdentityHasher = Microsoft.AspNetCore.Identity.PasswordHasher<LedgerLite.Domain.Users.User>;

namespace LedgerLite.Infrastructure.Security;

/// <summary>
/// Adapts ASP.NET Core Identity's PBKDF2 password hasher to the application abstraction.
/// We never roll our own crypto.
/// </summary>
internal sealed class PasswordHasher : IPasswordHasher
{
    private static readonly IdentityHasher Hasher = new();

    public string Hash(string password) => Hasher.HashPassword(user: null!, password);

    public bool Verify(string password, string hash) =>
        Hasher.VerifyHashedPassword(user: null!, hashedPassword: hash, providedPassword: password)
            != Microsoft.AspNetCore.Identity.PasswordVerificationResult.Failed;
}
