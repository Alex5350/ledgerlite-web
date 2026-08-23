using ErrorOr;
using FluentValidation;
using LedgerLite.Application.Abstractions;
using LedgerLite.Application.Common;
using LedgerLite.Domain.Users;
using LedgerLite.Domain.ValueObjects;

namespace LedgerLite.Application.Features.Users;

public sealed record RegisterUserCommand(string Email, string DisplayName, string Password);

public sealed record RegisterUserResult(Guid Id);

public sealed class RegisterUserValidator : AbstractValidator<RegisterUserCommand>
{
    public RegisterUserValidator()
    {
        RuleFor(x => x.DisplayName)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8)
            .MaximumLength(128);
    }
}

public sealed class RegisterUserHandler(
    IUserRepository users,
    IPasswordHasher passwordHasher,
    IValidator<RegisterUserCommand> validator) : ICommandHandler<RegisterUserCommand, RegisterUserResult>
{
    public async Task<ErrorOr<RegisterUserResult>> Handle(
        RegisterUserCommand command,
        CancellationToken cancellationToken = default)
    {
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ErrorsOrEmpty;
        }

        if (!EmailAddress.TryCreate(command.Email, out var email, out var emailError))
        {
            return Error.Validation("Users.InvalidEmail", emailError);
        }

        if (await users.EmailExistsAsync(email, cancellationToken))
        {
            return DomainErrors.Users.EmailAlreadyInUse;
        }

        var user = User.Create(email, command.DisplayName, passwordHasher.Hash(command.Password));
        await users.AddAsync(user, cancellationToken);

        return new RegisterUserResult(user.Id);
    }
}
