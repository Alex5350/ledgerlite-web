using ErrorOr;
using LedgerLite.Application.Abstractions;
using LedgerLite.Application.Common;
using LedgerLite.Application.Features.Users;
using LedgerLite.Domain.Users;
using LedgerLite.Domain.ValueObjects;

namespace LedgerLite.Application.UnitTests.Users;

public sealed class RegisterUserHandlerTests
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly RegisterUserHandler _handler;

    public RegisterUserHandlerTests()
    {
        _hasher.Hash(Arg.Any<string>()).Returns("hashed-password");
        _users.EmailExistsAsync(Arg.Any<EmailAddress>(), Arg.Any<CancellationToken>()).Returns(false);
        _handler = new RegisterUserHandler(_users, _hasher, new RegisterUserValidator());
    }

    private static RegisterUserCommand ValidCommand(string? email = "jane@example.com") =>
        new(email!, "Jane Doe", "Password123!");

    [Fact]
    public async Task Handle_WithValidCommand_HashesPasswordAndAddsUser()
    {
        User? added = null;
        _ = _users.AddAsync(Arg.Do<User>(user => added = user), Arg.Any<CancellationToken>());

        var result = await _handler.Handle(ValidCommand());

        Assert.False(result.IsError);
        Assert.NotNull(added);
        Assert.Equal(added!.Id, result.Value.Id);
        Assert.Equal("jane@example.com", added.Email.Value);
        Assert.Equal("Jane Doe", added.DisplayName);
        Assert.Equal("hashed-password", added.PasswordHash);
        _hasher.Received(1).Hash("Password123!");
        await _users.Received(1).AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithDuplicateEmail_ReturnsConflictError()
    {
        _users.EmailExistsAsync(Arg.Any<EmailAddress>(), Arg.Any<CancellationToken>()).Returns(true);

        var result = await _handler.Handle(ValidCommand());

        var error = result.FirstErrorOrThrow();
        Assert.Equal(ErrorType.Conflict, error.Type);
        Assert.Equal(DomainErrors.Users.EmailAlreadyInUse.Code, error.Code);
        await _users.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithInvalidEmail_ReturnsValidationError()
    {
        var result = await _handler.Handle(ValidCommand(email: "not-an-email"));

        var error = result.FirstErrorOrThrow();
        Assert.Equal(ErrorType.Validation, error.Type);
        Assert.Equal("Users.InvalidEmail", error.Code);
        Assert.False(string.IsNullOrWhiteSpace(error.Description));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_WithMissingDisplayName_ReturnsValidationError(string? displayName)
    {
        var result = await _handler.Handle(new RegisterUserCommand("jane@example.com", displayName!, "Password123!"));

        var error = result.FirstErrorOrThrow();
        Assert.Equal(ErrorType.Validation, error.Type);
        Assert.Equal("DisplayName", error.Code);
    }

    [Fact]
    public async Task Handle_WithOverlyLongDisplayName_ReturnsValidationError()
    {
        var command = new RegisterUserCommand("jane@example.com", new string('a', 101), "Password123!");

        var result = await _handler.Handle(command);

        Assert.Equal("DisplayName", result.FirstErrorOrThrow().Code);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("1234567")]
    public async Task Handle_WithInvalidPassword_ReturnsValidationError(string? password)
    {
        var result = await _handler.Handle(new RegisterUserCommand("jane@example.com", "Jane Doe", password!));

        var error = result.FirstErrorOrThrow();
        Assert.Equal(ErrorType.Validation, error.Type);
        Assert.Equal("Password", error.Code);
        await _users.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }
}
