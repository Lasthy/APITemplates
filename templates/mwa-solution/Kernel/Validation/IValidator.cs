namespace Kernel.Validation;

/// <summary>
/// Validates a request of type <typeparamref name="T"/> before handler execution.
/// Auto-discovered from module assemblies during AddModule.
/// </summary>
/// <typeparam name="T">The type of request to validate.</typeparam>
public interface IValidator<in T>
{
    /// <summary>
    /// Validates the specified request.
    /// </summary>
    /// <param name="request">The request to validate.</param>
    /// <returns>A <see cref="ValidationResult"/> indicating success or containing field errors.</returns>
    ValidationResult Validate(T request);
}
