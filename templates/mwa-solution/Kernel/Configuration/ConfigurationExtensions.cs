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
        var environment = builder.Environment.EnvironmentName;
        var dynamicSettings = new DynamicSettings(jsonSerializer, environment);

        // Register DynamicSettings into ASP.NET Configuration pipeline
        ((IConfigurationBuilder)builder.Configuration).AddDynamicSettings(dynamicSettings);

        // Register root host settings (appsettings.json, appsettings.{Environment}.json)
        var hostSettings = new HostSettings(jsonSerializer, environment);
        dynamicSettings.AddSection(hostSettings);

        // Register core dynamic settings sections
        var applicationSettings = new ApplicationSettings(jsonSerializer, environment);
        dynamicSettings.AddSection(applicationSettings);

        var instanceSettings = new InstanceSettings(jsonSerializer, environment);
        dynamicSettings.AddSection(instanceSettings);

        var messagingSettings = new MessagingSettings(jsonSerializer, environment);
        dynamicSettings.AddSection(messagingSettings);

        var emailSettings = new EmailSettings(jsonSerializer, environment);
        dynamicSettings.AddSection(emailSettings);

        var cacheSettings = new CacheSettings(jsonSerializer, environment);
        dynamicSettings.AddSection(cacheSettings);

        var encryptionSettings = new EncryptionSettings(jsonSerializer, environment);
        dynamicSettings.AddSection(encryptionSettings);

        var versioningSettings = new VersioningSettings(jsonSerializer, environment);
        dynamicSettings.AddSection(versioningSettings);

        var fileStorageSettings = new FileStorageSettings(jsonSerializer, environment);
        dynamicSettings.AddSection(fileStorageSettings);

        var notificationSettings = new NotificationSettings(jsonSerializer, environment);
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
