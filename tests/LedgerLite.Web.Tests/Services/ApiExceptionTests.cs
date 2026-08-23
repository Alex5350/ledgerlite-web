using LedgerLite.Web.Client.Services.Api;

namespace LedgerLite.Web.Tests.Services;

public sealed class ApiExceptionTests
{
    [Fact]
    public void PrimaryError_prefers_first_validation_message_over_detail_and_title()
    {
        var exception = new ApiException(
            statusCode: 400,
            title: "One or more validation errors occurred.",
            detail: "The entry is invalid.",
            errors: new Dictionary<string, string[]>
            {
                ["JournalEntries.SomeRule"] = ["Debits must equal credits.", "A second message."],
                ["JournalEntries.OtherRule"] = ["A later message."],
            });

        Assert.Equal("Debits must equal credits.", exception.PrimaryError);
    }

    [Fact]
    public void PrimaryError_falls_back_to_detail_when_no_validation_messages()
    {
        var exception = new ApiException(409, "Periods.AlreadyClosed", detail: "The period is already closed.");

        Assert.Equal("The period is already closed.", exception.PrimaryError);
    }

    [Fact]
    public void PrimaryError_falls_back_to_title_when_no_detail()
    {
        var exception = new ApiException(401, "Auth.InvalidCredentials");

        Assert.Equal("Auth.InvalidCredentials", exception.PrimaryError);
    }

    [Fact]
    public void PrimaryError_falls_back_to_detail_when_validation_messages_are_whitespace()
    {
        var exception = new ApiException(
            400,
            "Validation",
            detail: "Useful detail",
            errors: new Dictionary<string, string[]> { ["Code"] = ["  ", ""] });

        Assert.Equal("Useful detail", exception.PrimaryError);
    }

    [Fact]
    public void Constructor_exposes_problem_details_fields()
    {
        var errors = new Dictionary<string, string[]> { ["Code"] = ["Message"] };
        var exception = new ApiException(429, "TooManyRequests", detail: "Slow down.", errors: errors);

        Assert.Equal(429, exception.StatusCode);
        Assert.Equal("TooManyRequests", exception.Title);
        Assert.Equal("Slow down.", exception.Detail);
        Assert.Same(errors, exception.Errors);
    }

    [Fact]
    public void Constructor_defaults_errors_to_empty()
    {
        var exception = new ApiException(500, "Server");

        Assert.Empty(exception.Errors);
        Assert.Empty(exception.ErrorMessages);
        Assert.Equal("Server", exception.PrimaryError);
    }

    [Fact]
    public void ErrorMessages_flattens_all_codes_and_skips_whitespace()
    {
        var exception = new ApiException(
            400,
            "Validation",
            errors: new Dictionary<string, string[]>
            {
                ["A"] = ["first", "  "],
                ["B"] = ["second"],
            });

        Assert.Equal(["first", "second"], exception.ErrorMessages.ToArray());
    }

    [Fact]
    public void Message_contains_status_and_first_error()
    {
        var exception = new ApiException(
            400,
            "Validation",
            errors: new Dictionary<string, string[]> { ["A"] = ["Debits must equal credits."] });

        Assert.Equal("HTTP 400: Debits must equal credits.", exception.Message);
    }
}
