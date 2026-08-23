using LedgerLite.Api.Extensions;
using LedgerLite.Application.Abstractions;
using LedgerLite.Application.Features.Users;
using LedgerLite.Domain.ValueObjects;
using LedgerLite.Infrastructure.Authentication;

namespace LedgerLite.Api.Features.Auth;

public sealed record RegisterRequest(string Email, string DisplayName, string Password);

public sealed record LoginRequest(string Email, string Password);

public sealed record RegisterResponse(Guid Id);

public sealed record LoginResponse(string AccessToken, DateTime ExpiresAtUtc, Guid UserId, string Email);

internal static class AuthEndpoints
{
    /// <summary>Hash of a decoy password, used to equalize timing when the user does not exist.</summary>
    private static string? _decoyHash;

    public static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/register", RegisterUser)
            .WithName("Auth_RegisterUser")
            .WithSummary("Register a new user");

        group.MapPost("/login", Login)
            .WithName("Auth_Login")
            .WithSummary("Log in and obtain a JWT access token")
            .RequireRateLimiting("auth-login");

        return group;
    }

    private static async Task<IResult> RegisterUser(
        RegisterRequest request,
        ICommandHandler<RegisterUserCommand, RegisterUserResult> handler,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        var command = new RegisterUserCommand(request.Email, request.DisplayName, request.Password);

        var result = await handler.Handle(command, cancellationToken);

        return await result.ToResponseAsync(async registered =>
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return TypedResults.Created($"/api/users/{registered.Id}", new RegisterResponse(registered.Id));
        });
    }

    private static async Task<IResult> Login(
        LoginRequest request,
        IUserRepository users,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator tokenGenerator,
        CancellationToken cancellationToken)
    {
        if (!EmailAddress.TryCreate(request.Email, out var email, out _))
        {
            return InvalidCredentials();
        }

        var user = await users.GetByEmailAsync(email, cancellationToken);

        // Always run one hash verification to avoid leaking whether the email exists.
        _decoyHash ??= passwordHasher.Hash("timing-safe-decoy-password");
        var hashToVerify = user?.PasswordHash ?? _decoyHash;

        if (user is null || !passwordHasher.Verify(request.Password, hashToVerify))
        {
            return InvalidCredentials();
        }

        var token = tokenGenerator.Generate(user.Id, user.Email.Value);

        return TypedResults.Ok(new LoginResponse(
            AccessToken: token.AccessToken,
            ExpiresAtUtc: token.ExpiresAtUtc,
            UserId: user.Id,
            Email: user.Email.Value));
    }

    private static IResult InvalidCredentials() => TypedResults.Problem(
        statusCode: StatusCodes.Status401Unauthorized,
        title: "Auth.InvalidCredentials",
        detail: "Email or password is incorrect.");
}
