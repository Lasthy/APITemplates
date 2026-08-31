namespace Kernel.Validation;

/// <summary>
/// Exception thrown or wrapped in a <see cref="Kernel.Common.Result.Failure"/> when validation fails.
/// </summary>
public class ValidationException : Exception
{
    /// <summary>
    /// The collection of validation errors that caused this exception.
    /// </summary>
    public IReadOnlyList<ValidationError> Errors { get; }

    /// <summary>
    /// The structured validation result.
    /// </summary>
    public ValidationResult Result { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="ValidationException"/> from a <see cref="ValidationResult"/>.
    /// </summary>
    /// <param name="result">The validation result containing field errors.</param>
    public ValidationException(ValidationResult result)
        : base(FormatMessage(result?.Errors ?? Array.Empty<ValidationError>()))
    {
        Result = result ?? ValidationResult.Success();
        Errors = Result.Errors;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="ValidationException"/> from a collection of errors.
    /// </summary>
    /// <param name="errors">The collection of validation errors.</param>
    public ValidationException(IEnumerable<ValidationError> errors)
        : this(new ValidationResult(errors))
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="ValidationException"/> for a single field error.
    /// </summary>
    /// <param name="field">The name of the invalid field.</param>
    /// <param name="message">The validation error message.</param>
    public ValidationException(string field, string message)
        : this(ValidationResult.Failure(field, message))
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="ValidationException"/> with a general error message.
    /// </summary>
    /// <param name="message">The error message.</param>
    public ValidationException(string message)
        : base(message)
    {
        Errors = Array.Empty<ValidationError>();
        Result = ValidationResult.Success();
    }

    private static string FormatMessage(IEnumerable<ValidationError> errors)
    {
        var errorList = errors.ToList();
        if (errorList.Count == 0)
        {
            return "Validation failed.";
        }

        var formatted = string.Join("; ", errorList.Select(e =>
            string.IsNullOrWhiteSpace(e.Field) ? e.Message : $"{e.Field}: {e.Message}"));

        return $"Validation failed: {formatted}";
    }
}
