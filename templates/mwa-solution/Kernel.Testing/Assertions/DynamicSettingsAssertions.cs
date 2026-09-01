using Kernel.Configuration;
using Xunit;

namespace Kernel.Testing.Assertions;

/// <summary>
/// Fluent assertion extensions for <see cref="DynamicSettings"/> and <see cref="DynamicSettingsSection"/>.
/// </summary>
public static class DynamicSettingsAssertions
{
    /// <summary>
    /// Asserts that the <see cref="DynamicSettings"/> instance has the specified environment name.
    /// </summary>
    public static void ShouldHaveEnvironment(
        this DynamicSettings dynamicSettings,
        string expectedEnvironment,
        string? userMessage = null)
    {
        ArgumentNullException.ThrowIfNull(dynamicSettings);
        Assert.True(
            string.Equals(dynamicSettings.Environment, expectedEnvironment, StringComparison.OrdinalIgnoreCase),
            userMessage ?? $"Expected DynamicSettings environment to be '{expectedEnvironment}', but found '{dynamicSettings.Environment}'.");
    }

    /// <summary>
    /// Asserts that the <see cref="DynamicSettingsSection"/> has the specified environment name.
    /// </summary>
    public static void ShouldHaveEnvironment(
        this DynamicSettingsSection section,
        string expectedEnvironment,
        string? userMessage = null)
    {
        ArgumentNullException.ThrowIfNull(section);
        Assert.True(
            string.Equals(section.EnvironmentName, expectedEnvironment, StringComparison.OrdinalIgnoreCase),
            userMessage ?? $"Expected section '{section.SectionName}' environment to be '{expectedEnvironment}', but found '{section.EnvironmentName}'.");
    }

    /// <summary>
    /// Asserts that the <see cref="DynamicSettings"/> registry contains a section with the given name.
    /// </summary>
    public static void ShouldContainSection(
        this DynamicSettings dynamicSettings,
        string sectionName,
        string? userMessage = null)
    {
        ArgumentNullException.ThrowIfNull(dynamicSettings);
        Assert.True(
            dynamicSettings.TryGetSection(sectionName, out _),
            userMessage ?? $"Expected DynamicSettings to contain section '{sectionName}', but it was not found.");
    }

    /// <summary>
    /// Asserts that the <see cref="DynamicSettings"/> registry contains a section of type <typeparamref name="T"/>.
    /// </summary>
    public static void ShouldContainSection<T>(
        this DynamicSettings dynamicSettings,
        string? userMessage = null) where T : DynamicSettingsSection
    {
        ArgumentNullException.ThrowIfNull(dynamicSettings);
        bool found = false;
        try
        {
            var section = dynamicSettings.GetSection<T>();
            found = section != null;
        }
        catch
        {
            found = false;
        }

        Assert.True(
            found,
            userMessage ?? $"Expected DynamicSettings to contain section of type '{typeof(T).Name}', but it was not found.");
    }

    /// <summary>
    /// Asserts that a section contains the specified setting key and value.
    /// </summary>
    public static void ShouldHaveSetting<T>(
        this DynamicSettingsSection section,
        string key,
        T expectedValue,
        string? userMessage = null)
    {
        ArgumentNullException.ThrowIfNull(section);
        Assert.True(
            section.TryGetValue<T>(key, out var actual),
            userMessage ?? $"Expected section '{section.SectionName}' to contain key '{key}', but it was not found.");

        Assert.Equal(expectedValue, actual);
    }
}
