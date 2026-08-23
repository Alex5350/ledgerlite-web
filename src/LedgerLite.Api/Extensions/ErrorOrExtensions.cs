using ErrorOr;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LedgerLite.Api.Extensions;

/// <summary>
/// Maps <see cref="ErrorOr{T}"/> results onto TypedResults: validation errors become
/// ValidationProblem, NotFound/Conflict/Unauthorized become ProblemDetails with the
/// matching status code.
/// </summary>
internal static class ErrorOrExtensions
{
    public static IResult ToResponse<T>(this ErrorOr<T> result, Func<T, IResult> onSuccess) =>
        result.Match(
            value => onSuccess(value),
            errors => errors.ToProblem());

    public static async Task<IResult> ToResponseAsync<T>(this ErrorOr<T> result, Func<T, Task<IResult>> onSuccess) =>
        await result.Match(
            async value => await onSuccess(value),
            errors => Task.FromResult(errors.ToProblem()));

    public static IResult ToProblem(this IReadOnlyList<Error> errors)
    {
        if (errors.Count == 0)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Unknown error.");
        }

        if (errors.All(error => error.Type == ErrorType.Validation))
        {
            return TypedResults.ValidationProblem(errors.ToValidationDictionary());
        }

        var primary = errors[0];
        var statusCode = primary.Type switch
        {
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status500InternalServerError
        };

        return TypedResults.Problem(
            statusCode: statusCode,
            title: primary.Code,
            detail: primary.Description,
            extensions: new Dictionary<string, object?>
            {
                ["errors"] = errors.Select(error => error.Code).ToArray()
            });
    }

    private static IDictionary<string, string[]> ToValidationDictionary(this IEnumerable<Error> errors) =>
        errors
            .GroupBy(error => error.Code, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.Description).ToArray(),
                StringComparer.Ordinal);
}
