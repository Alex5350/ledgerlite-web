using LedgerLite.Domain.ValueObjects;

namespace LedgerLite.Domain.UnitTests.ValueObjects;

public sealed class EmailAddressTests
{
    [Theory]
    [InlineData("user@example.com", "user@example.com")]
    [InlineData("  User@Example.COM  ", "user@example.com")]
    [InlineData("MiXeD@MaIl.Co.UK", "mixed@mail.co.uk")]
    [InlineData("first.last+tag@example.io", "first.last+tag@example.io")]
    [InlineData("a@b.c", "a@b.c")]
    public void TryCreate_WithValidEmail_NormalizesToLowercaseAndTrims(string input, string expected)
    {
        var created = EmailAddress.TryCreate(input, out var email, out var error);

        Assert.True(created);
        Assert.Null(error);
        Assert.Equal(expected, email.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("plainaddress")]        // no '@'
    [InlineData("@example.com")]        // missing local part
    [InlineData("user@")]               // missing domain
    [InlineData("user@example")]        // domain without a dot
    [InlineData("user@@example.com")]   // multiple '@'
    public void TryCreate_WithInvalidEmail_Fails(string? input)
    {
        var created = EmailAddress.TryCreate(input, out var email, out var error);

        Assert.False(created);
        Assert.Equal(default, email);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void TryCreate_WhenEmailExceeds254Characters_Fails()
    {
        var tooLong = new string('a', 250) + "@x.com";

        var created = EmailAddress.TryCreate(tooLong, out _, out var error);

        Assert.False(created);
        Assert.Contains("254", error);
    }

    [Fact]
    public void Create_WithValidInput_ReturnsEmail()
    {
        Assert.Equal("user@example.com", EmailAddress.Create("USER@Example.com").Value);
    }

    [Fact]
    public void Create_WithInvalidInput_Throws()
    {
        Assert.Throws<ArgumentException>(() => EmailAddress.Create("not-an-email"));
    }

    [Fact]
    public void Equality_IsCaseInsensitiveThroughNormalization()
    {
        Assert.True(EmailAddress.Create("A@B.com") == EmailAddress.Create("a@b.COM"));
        Assert.NotEqual(EmailAddress.Create("a@b.com"), EmailAddress.Create("c@b.com"));
    }
}
