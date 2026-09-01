using Kernel.Caching;
using Kernel.Email;
using Kernel.Encryption;
using Kernel.Json;
using Kernel.Messaging;
using Kernel.Notifications;
using Kernel.Storage;
using Kernel.Versioning;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kernel.Configuration;

/// <summary>
/// Extension methods for configuring and registering <see cref="DynamicSettings"/> and its sections.
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

    /// <summary>
    /// Registers <see cref="DynamicSettings"/>, wires it into the configuration pipeline,
    /// and registers all standard kernel dynamic settings sections.
    /// </summary>
    public static DynamicSettings AddKernelDynamicSettings(this WebApplicationBuilder builder, IJsonSerializer jsonSerializer)
    {
        var initialEnv = builder.Environment.EnvironmentName;

        // Check environment variables first (highest priority)
        var envVar = System.Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? System.Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? System.Environment.GetEnvironmentVariable("Application__Environment")
            ?? System.Environment.GetEnvironmentVariable("APPSETTINGS__Application__Environment")
            ?? System.Environment.GetEnvironmentVariable("SETTINGS__Application__Environment");

        string effectiveEnvironment;

        if (!string.IsNullOrWhiteSpace(envVar))
        {
            effectiveEnvironment = envVar;
        }
        else if (!string.IsNullOrWhiteSpace(initialEnv) && !string.Equals(initialEnv, "Production", StringComparison.OrdinalIgnoreCase))
        {
            effectiveEnvironment = initialEnv;
        }
        else
        {
            var appSettings = new ApplicationSettings(jsonSerializer, initialEnv);
            effectiveEnvironment = !string.IsNullOrWhiteSpace(appSettings.Environment)
                ? appSettings.Environment
                : initialEnv;
        }

        // Update builder.Environment.EnvironmentName so ASP.NET Core host environment matches!
        builder.Environment.EnvironmentName = effectiveEnvironment;

        var dynamicSettings = new DynamicSettings(jsonSerializer, effectiveEnvironment);

        // Register DynamicSettings into ASP.NET Configuration pipeline
        ((IConfigurationBuilder)builder.Configuration).AddDynamicSettings(dynamicSettings);

        // Register root host settings (appsettings.json, appsettings.{Environment}.json)
        var hostSettings = new HostSettings(jsonSerializer, effectiveEnvironment);
        dynamicSettings.AddSection(hostSettings);

        // Register core dynamic settings sections
        var applicationSettings = new ApplicationSettings(jsonSerializer, effectiveEnvironment);
        dynamicSettings.AddSection(applicationSettings);

        var instanceSettings = new InstanceSettings(jsonSerializer, effectiveEnvironment);
        dynamicSettings.AddSection(instanceSettings);

        var messagingSettings = new MessagingSettings(jsonSerializer, effectiveEnvironment);
        dynamicSettings.AddSection(messagingSettings);

        var emailSettings = new EmailSettings(jsonSerializer, effectiveEnvironment);
        dynamicSettings.AddSection(emailSettings);

        var cacheSettings = new CacheSettings(jsonSerializer, effectiveEnvironment);
        dynamicSettings.AddSection(cacheSettings);

        var encryptionSettings = new EncryptionSettings(jsonSerializer, effectiveEnvironment);
        dynamicSettings.AddSection(encryptionSettings);

        var versioningSettings = new VersioningSettings(jsonSerializer, effectiveEnvironment);
        dynamicSettings.AddSection(versioningSettings);

        var fileStorageSettings = new FileStorageSettings(jsonSerializer, effectiveEnvironment);
        dynamicSettings.AddSection(fileStorageSettings);

        var notificationSettings = new NotificationSettings(jsonSerializer, effectiveEnvironment);
        dynamicSettings.AddSection(notificationSettings);

        // Register all sections as singletons in DI
        builder.Services.AddSingleton(dynamicSettings);
        builder.Services.AddSingleton(hostSettings);
        builder.Services.AddSingleton(applicationSettings);
        builder.Services.AddSingleton(instanceSettings);
        builder.Services.AddSingleton(messagingSettings);
        builder.Services.AddSingleton(emailSettings);
        builder.Services.AddSingleton(cacheSettings);
        builder.Services.AddSingleton(encryptionSettings);
        builder.Services.AddSingleton(versioningSettings);
        builder.Services.AddSingleton(fileStorageSettings);
        builder.Services.AddSingleton(notificationSettings);

        return dynamicSettings;
    }
}
