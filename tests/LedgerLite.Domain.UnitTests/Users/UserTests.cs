using LedgerLite.Domain.Users;
using LedgerLite.Domain.ValueObjects;

namespace LedgerLite.Domain.UnitTests.Users;

public sealed class UserTests
{
    private static readonly EmailAddress Email = EmailAddress.Create("jane@example.com");

    [Fact]
    public void Create_WithValidInput_Succeeds()
    {
        var user = User.Create(Email, "Jane Doe", "hash");

        Assert.Equal(Email, user.Email);
        Assert.Equal("Jane Doe", user.DisplayName);
        Assert.Equal("hash", user.PasswordHash);
        Assert.NotEqual(Guid.Empty, user.Id);
    }

    [Fact]
    public void Create_TrimsDisplayName()
    {
        var user = User.Create(Email, "  Jane Doe  ", "hash");

        Assert.Equal("Jane Doe", user.DisplayName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithMissingDisplayName_Throws(string? displayName)
    {
        Assert.Throws<ArgumentException>(() => User.Create(Email, displayName!, "hash"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Create_WithMissingPasswordHash_Throws(string? passwordHash)
    {
        Assert.Throws<ArgumentException>(() => User.Create(Email, "Jane Doe", passwordHash!));
    }

    [Fact]
    public void Create_GeneratesUniqueIds()
    {
        var first = User.Create(Email, "Jane", "hash");
        var second = User.Create(Email, "Jane", "hash");

        Assert.NotEqual(first.Id, second.Id);
    }
}
