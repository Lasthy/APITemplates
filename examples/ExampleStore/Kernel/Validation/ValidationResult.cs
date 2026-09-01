namespace Kernel.Validation;

/// <summary>
/// Represents a single validation error on a specific field.
/// </summary>
/// <param name="Field">The name of the invalid property or parameter.</param>
/// <param name="Message">The description of the validation rule violation.</param>
public record ValidationError(string Field, string Message);

/// <summary>
/// Structured result containing the outcome of a validation operation and any field-level errors.
/// </summary>
public class ValidationResult
{
    /// <summary>
    /// The collection of validation errors encountered. Empty if valid.
    /// </summary>
    public IReadOnlyList<ValidationError> Errors { get; }

    /// <summary>
    /// Indicates whether validation succeeded (no errors were encountered).
    /// </summary>
    public bool IsValid => Errors.Count == 0;

    /// <summary>
    /// Initializes a successful <see cref="ValidationResult"/> with no errors.
    /// </summary>
    public ValidationResult() : this(Array.Empty<ValidationError>())
    {
    }

    /// <summary>
    /// Initializes a <see cref="ValidationResult"/> with the specified errors.
    /// </summary>
    /// <param name="errors">The validation errors.</param>
    public ValidationResult(IEnumerable<ValidationError> errors)
    {
        Errors = errors != null ? errors.ToList().AsReadOnly() : Array.Empty<ValidationError>();
    }

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    public static ValidationResult Success() => new(Array.Empty<ValidationError>());

    /// <summary>
    /// Creates a failed validation result with one or more validation errors.
    /// </summary>
    /// <param name="errors">The validation errors.</param>
    public static ValidationResult Failure(params ValidationError[] errors) => new(errors);

    /// <summary>
    /// Creates a failed validation result with a single field error.
    /// </summary>
    /// <param name="field">The name of the invalid field.</param>
    /// <param name="message">The validation error message.</param>
    public static ValidationResult Failure(string field, string message) => new([new(field, message)]);

    /// <summary>
    /// Creates a failed validation result with an enumerable collection of errors.
    /// </summary>
    /// <param name="errors">The validation errors.</param>
    public static ValidationResult Failure(IEnumerable<ValidationError> errors) => new(errors);
}
