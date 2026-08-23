using ErrorOr;
using FluentValidation.Results;

namespace LedgerLite.Application.Common;

public static class ValidationMappings
{
    // C# 14 extension block: maps a FluentValidation result to ErrorOr errors.
    extension(ValidationResult result)
    {
        public List<Error> ErrorsOrEmpty =>
            result.IsValid
                ? []
                : [.. result.Errors.Select(f => Error.Validation(f.PropertyName, f.ErrorMessage))];
    }
}
