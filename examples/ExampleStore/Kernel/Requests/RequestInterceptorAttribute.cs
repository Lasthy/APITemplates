using Microsoft.Extensions.DependencyInjection;

namespace Kernel.Requests;

/// <summary>
/// Base attribute for applying interceptors to request handlers.
/// Allows specifying execution order and configuration parameters.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
public abstract class RequestInterceptorAttribute : Attribute
{
    /// <summary>
    /// Execution order of the interceptor. Lower numbers run first (outermost in pipeline).
    /// Defaults to 0.
    /// </summary>
    public int Order { get; set; } = 0;

    /// <summary>
    /// Creates or resolves the interceptor instance using the provided <see cref="IServiceProvider"/>.
    /// </summary>
    public abstract IRequestInterceptor CreateInterceptor(IServiceProvider serviceProvider);
}

/// <summary>
/// Specifies that a request handler should be wrapped by the given <typeparamref name="TInterceptor"/>.
/// </summary>
/// <typeparam name="TInterceptor">The interceptor type to execute.</typeparam>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
public class InterceptAttribute<TInterceptor> : RequestInterceptorAttribute 
    where TInterceptor : IRequestInterceptor
{
    public override IRequestInterceptor CreateInterceptor(IServiceProvider serviceProvider)
    {
        return ActivatorUtilities.GetServiceOrCreateInstance<TInterceptor>(serviceProvider);
    }
}

/// <summary>
/// Specifies that a request handler should be wrapped by an interceptor of the specified type,
/// optionally passing constructor arguments.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
public class InterceptAttribute : RequestInterceptorAttribute
{
    /// <summary>
    /// The concrete interceptor type. Must implement <see cref="IRequestInterceptor"/>.
    /// </summary>
    public Type InterceptorType { get; }

    /// <summary>
    /// Optional constructor arguments to pass when creating the interceptor instance.
    /// </summary>
    public object[] Arguments { get; set; }

    public InterceptAttribute(Type interceptorType, params object[] arguments)
    {
        ArgumentNullException.ThrowIfNull(interceptorType);
        if (!typeof(IRequestInterceptor).IsAssignableFrom(interceptorType))
        {
            throw new ArgumentException(
                $"Type '{interceptorType.FullName}' must implement '{nameof(IRequestInterceptor)}'.",
                nameof(interceptorType));
        }

        InterceptorType = interceptorType;
        Arguments = arguments ?? [];
    }

    public override IRequestInterceptor CreateInterceptor(IServiceProvider serviceProvider)
    {
        if (Arguments == null || Arguments.Length == 0)
        {
            return (IRequestInterceptor)ActivatorUtilities.GetServiceOrCreateInstance(serviceProvider, InterceptorType);
        }

        return (IRequestInterceptor)ActivatorUtilities.CreateInstance(serviceProvider, InterceptorType, Arguments);
    }
}
