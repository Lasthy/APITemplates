using Microsoft.Extensions.Configuration;

namespace Kernel.Configuration;

/// <summary>
/// Extension methods for configuring <see cref="DynamicSettings"/> with <see cref="IConfigurationBuilder"/>.
/// </summary>
public static class ConfigurationExtensions
{
    /// <summary>
    /// Adds <see cref="DynamicSettings"/> as an <see cref="IConfigurationSource"/> to the <see cref="IConfigurationBuilder"/>.
    /// </summary>
    /// <param name="builder">The configuration builder.</param>
    /// <param name="dynamicSettings">The dynamic settings instance.</param>
    /// <returns>The configuration builder for chaining.</returns>
    public static IConfigurationBuilder AddDynamicSettings(
        this IConfigurationBuilder builder,
        DynamicSettings dynamicSettings)
    {
        return builder.Add(new DynamicSettingsConfigurationSource(dynamicSettings));
    }
}
