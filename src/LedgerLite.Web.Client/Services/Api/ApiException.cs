using System.Net.Http;
using System.Text.Json;

namespace LedgerLite.Web.Client.Services.Api;

/// <summary>
/// Thrown by <see cref="ILedgerLiteApiClient"/> for any non-success HTTP response.
/// Carries the parsed RFC 9457 ProblemDetails payload: validation problems expose
/// an "errors" dictionary of code -> messages, while conflict/authorization problems
/// expose a "title" (the domain error code) and a human-readable "detail".
/// </summary>
public sealed class ApiException : Exception
{
    private static readonly IReadOnlyDictionary<string, string[]> EmptyErrors = new Dictionary<string, string[]>();

    public ApiException(int statusCode, string title, string? detail = null, IReadOnlyDictionary<string, string[]>? errors = null)
        : base(BuildMessage(statusCode, title, detail, errors))
    {
        StatusCode = statusCode;
        Title = title;
        Detail = detail;
        Errors = errors ?? EmptyErrors;
    }

    /// <summary>HTTP status code of the failed response (400, 401, 409, 429, ...).</summary>
    public int StatusCode { get; }

    /// <summary>ProblemDetails title: a domain error code ("Auth.InvalidCredentials") or a generic phrase.</summary>
    public string Title { get; }

    /// <summary>ProblemDetails detail: human-readable description, when present.</summary>
    public string? Detail { get; }

    /// <summary>Validation errors keyed by error code ("JournalEntries.NotBalanced" -> ["..."]). Empty when none.</summary>
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    /// <summary>All individual validation messages, flattened.</summary>
    public IEnumerable<string> ErrorMessages =>
        Errors.Values.SelectMany(messages => messages).Where(message => !string.IsNullOrWhiteSpace(message));

    /// <summary>The single best message to show a user: first validation message, else detail, else title.</summary>
    public string PrimaryError => ErrorMessages.FirstOrDefault() ?? Detail ?? Title;

    /// <summary>
    /// Builds an exception by parsing a ProblemDetails (or ValidationProblem) response body.
    /// Handles both error shapes produced by the API: <c>errors</c> as an object (400) and
    /// <c>errors</c> as an array of codes (401/404/409/429).
    /// </summary>
    public static async Task<ApiException> FromResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken = default)
    {
        var statusCode = (int)response.StatusCode;
        string? title = null;
        string? detail = null;
        Dictionary<string, string[]>? errors = null;

        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = document.RootElement;

            if (root.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty("title", out var titleElement) && titleElement.ValueKind == JsonValueKind.String)
                {
                    title = titleElement.GetString();
                }

                if (root.TryGetProperty("detail", out var detailElement) && detailElement.ValueKind == JsonValueKind.String)
                {
                    detail = detailElement.GetString();
                }

                if (root.TryGetProperty("errors", out var errorsElement))
                {
                    errors = ParseErrors(errorsElement, detail);
                }
            }
        }
        catch (JsonException)
        {
            // Body was not valid ProblemDetails JSON; fall back to the reason phrase below.
        }

        title ??= response.ReasonPhrase ?? $"HTTP {statusCode}";
        return new ApiException(statusCode, title, detail, errors);
    }

    private static Dictionary<string, string[]>? ParseErrors(JsonElement errorsElement, string? detail)
    {
        if (errorsElement.ValueKind == JsonValueKind.Object)
        {
            var result = new Dictionary<string, string[]>(StringComparer.Ordinal);
            foreach (var property in errorsElement.EnumerateObject())
            {
                string[] messages = property.Value.ValueKind == JsonValueKind.Array
                    ? [.. property.Value.EnumerateArray()
                        .Where(item => item.ValueKind == JsonValueKind.String)
                        .Select(item => item.GetString() ?? string.Empty)]
                    : [];

                result[property.Name] = messages;
            }

            return result;
        }

        if (errorsElement.ValueKind == JsonValueKind.Array)
        {
            // Non-validation problems attach an "errors" extension with just the error codes.
            var result = new Dictionary<string, string[]>(StringComparer.Ordinal);
            foreach (var item in errorsElement.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String && item.GetString() is { Length: > 0 } code)
                {
                    result[code] = string.IsNullOrEmpty(detail) ? Array.Empty<string>() : [detail!];
                }
            }

            return result;
        }

        return null;
    }

    private static string BuildMessage(int statusCode, string title, string? detail, IReadOnlyDictionary<string, string[]>? errors)
    {
        var messages = errors?.Values.SelectMany(error => error).Where(message => !string.IsNullOrWhiteSpace(message)) ?? [];
        var body = messages.Any() ? string.Join("; ", messages) : detail ?? title;
        return $"HTTP {statusCode}: {body}";
    }
}
