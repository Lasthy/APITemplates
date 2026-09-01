using Microsoft.Extensions.DependencyInjection;

namespace Kernel.Email.Extensions;

/// <summary>
/// Extension methods for configuring and registering email delivery infrastructure.
/// </summary>
public static class EmailExtensions
{
    /// <summary>
    /// Registers email sender and template renderer services in the DI container.
    /// </summary>
    public static IServiceCollection AddEmail(this IServiceCollection services)
    {
        services.AddTransient<IEmailSender, SmtpEmailSender>();
        services.AddSingleton<IEmailTemplateRenderer, DefaultEmailTemplateRenderer>();
        return services;
    }
}
