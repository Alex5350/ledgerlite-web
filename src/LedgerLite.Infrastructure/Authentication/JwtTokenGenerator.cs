using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace LedgerLite.Infrastructure.Authentication;

/// <summary>JWT issuing options bound from the "Jwt" configuration section.</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required(AllowEmptyStrings = false)]
    [MaxLength(200)]
    public string Issuer { get; init; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    [MaxLength(200)]
    public string Audience { get; init; } = string.Empty;

    /// <summary>HMAC-SHA256 signing key; must be at least 32 characters (256 bits).</summary>
    [Required(AllowEmptyStrings = false)]
    [MinLength(32)]
    [MaxLength(512)]
    public string Key { get; init; } = string.Empty;

    [Range(1, 1440)]
    public int ExpiryMinutes { get; init; } = 60;
}

/// <summary>Issues JWT access tokens with sub/email claims signed with a symmetric key.</summary>
public interface IJwtTokenGenerator
{
    JwtTokenResult Generate(Guid userId, string email);
}

public sealed record JwtTokenResult(string AccessToken, DateTime ExpiresAtUtc);

internal sealed class JwtTokenGenerator(IOptions<JwtOptions> options) : IJwtTokenGenerator
{
    // JsonWebTokenHandler is thread-safe and avoids the legacy JwtSecurityTokenHandler.
    private static readonly JsonWebTokenHandler TokenHandler = new();

    public JwtTokenResult Generate(Guid userId, string email)
    {
        var settings = options.Value;
        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(settings.ExpiryMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = settings.Issuer,
            Audience = settings.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expires,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Key)),
                SecurityAlgorithms.HmacSha256),
            Claims = new Dictionary<string, object>
            {
                ["sub"] = userId.ToString(),
                ["email"] = email
            }
        };

        return new JwtTokenResult(AccessToken: TokenHandler.CreateToken(descriptor), ExpiresAtUtc: expires);
    }
}
