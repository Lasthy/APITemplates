using Kernel.Requests;

namespace Kernel.Validation;

/// <summary>
/// Applies validation to a request handler.
/// The dispatcher resolves registered <see cref="IValidator{T}"/> implementations and calls
/// <see cref="IValidator{T}.Validate"/> before the handler executes.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public class ValidateAttribute : RequestInterceptorAttribute
{
    public ValidateAttribute()
    {
        Order = -50; // Runs after authorization (-100), before logging (-10) / transaction (0)
    }

    public override IRequestInterceptor CreateInterceptor(IServiceProvider serviceProvider)
        => new ValidationInterceptor(serviceProvider);
}
