using Kernel.Spa;
using Xunit;

namespace Kernel.Testing.Assertions;

/// <summary>
/// Fluent assertion extensions for <see cref="SpaRegistry"/>, <see cref="SpaAppOptions"/>, and <see cref="SpaSettings"/>.
/// </summary>
public static class SpaAssertions
{
    /// <summary>
    /// Asserts that the registry contains an SPA application with the specified name.
    /// </summary>
    public static SpaAppOptions ShouldHaveApp(
        this SpaRegistry registry,
        string appName,
        string? userMessage = null)
    {
        ArgumentNullException.ThrowIfNull(registry);
        Assert.True(
            registry.TryGetApp(appName, out var app),
            userMessage ?? $"Expected SpaRegistry to contain app '{appName}', but it was not found.");

        return app!;
    }

    /// <summary>
    /// Asserts that the registry contains an SPA with the specified name and base path.
    /// </summary>
    public static SpaAppOptions ShouldHaveAppWithBasePath(
        this SpaRegistry registry,
        string appName,
        string expectedBasePath,
        string? userMessage = null)
    {
        var app = registry.ShouldHaveApp(appName, userMessage);
        Assert.Equal(SpaAppOptions.NormalizeBasePath(expectedBasePath), app.BasePath);
        return app;
    }

    /// <summary>
    /// Asserts that the registry has exactly <paramref name="expectedCount"/> registered SPAs.
    /// </summary>
    public static void ShouldHaveAppCount(
        this SpaRegistry registry,
        int expectedCount,
        string? userMessage = null)
    {
        ArgumentNullException.ThrowIfNull(registry);
        Assert.Equal(expectedCount, registry.Apps.Count);
    }

    /// <summary>
    /// Asserts that the ordered apps list matches the expected sequence of app names (ordered by prefix length descending).
    /// </summary>
    public static void ShouldHaveEvaluationOrder(
        this SpaRegistry registry,
        params string[] expectedAppNames)
    {
        ArgumentNullException.ThrowIfNull(registry);
        var orderedNames = registry.GetOrderedApps().Select(a => a.Name).ToArray();
        Assert.Equal(expectedAppNames, orderedNames);
    }

    /// <summary>
    /// Asserts that the SPA option has the specified base path.
    /// </summary>
    public static SpaAppOptions ShouldHaveBasePath(
        this SpaAppOptions app,
        string expectedBasePath)
    {
        ArgumentNullException.ThrowIfNull(app);
        Assert.Equal(SpaAppOptions.NormalizeBasePath(expectedBasePath), app.BasePath);
        return app;
    }

    /// <summary>
    /// Asserts that the SPA option has the specified dev server URL.
    /// </summary>
    public static SpaAppOptions ShouldHaveDevServerUrl(
        this SpaAppOptions app,
        string? expectedUrl)
    {
        ArgumentNullException.ThrowIfNull(app);
        Assert.Equal(expectedUrl, app.DevServerUrl);
        return app;
    }

    /// <summary>
    /// Asserts that the SPA option has the specified owner.
    /// </summary>
    public static SpaAppOptions ShouldHaveOwner(
        this SpaAppOptions app,
        string expectedOwner)
    {
        ArgumentNullException.ThrowIfNull(app);
        Assert.Equal(expectedOwner, app.Owner);
        return app;
    }

    /// <summary>
    /// Asserts that the SPA option has the specified dist path.
    /// </summary>
    public static SpaAppOptions ShouldHaveDistPath(
        this SpaAppOptions app,
        string expectedDistPath)
    {
        ArgumentNullException.ThrowIfNull(app);
        Assert.Equal(expectedDistPath, app.DistPath);
        return app;
    }

    /// <summary>
    /// Asserts that the SPA option has the specified dev server command.
    /// </summary>
    public static SpaAppOptions ShouldHaveDevServerCommand(
        this SpaAppOptions app,
        string? expectedCommand)
    {
        ArgumentNullException.ThrowIfNull(app);
        Assert.Equal(expectedCommand, app.DevServerCommand);
        return app;
    }

    /// <summary>
    /// Asserts that the SPA option has auto-start dev server enabled or disabled.
    /// </summary>
    public static SpaAppOptions ShouldAutoStartDevServer(
        this SpaAppOptions app,
        bool expectedValue = true)
    {
        ArgumentNullException.ThrowIfNull(app);
        Assert.Equal(expectedValue, app.AutoStartDevServer);
        return app;
    }

    /// <summary>
    /// Asserts that SPA settings is enabled.
    /// </summary>
    public static void ShouldBeEnabled(this SpaSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Assert.True(settings.Enabled, "Expected SpaSettings to be enabled, but it was disabled.");
    }

    /// <summary>
    /// Asserts that the SPA settings excludes the specified URL prefix.
    /// </summary>
    public static void ShouldExcludePrefix(this SpaSettings settings, string prefix)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Assert.Contains(prefix, settings.ExcludedPrefixes);
    }
}
