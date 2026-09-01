using Xunit;

namespace Kernel.Testing.Assertions;

/// <summary>
/// Fluent assertion extensions for API versioning headers on <see cref="HttpResponseMessage"/>.
/// </summary>
public static class ApiVersioningAssertions
{
    /// <summary>
    /// Asserts that the response contains the <c>api-supported-versions</c> header with the specified versions.
    /// </summary>
    public static void ShouldHaveSupportedApiVersions(
        this HttpResponseMessage response,
        params string[] expectedVersions)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(expectedVersions);

        Assert.True(
            response.Headers.TryGetValues("api-supported-versions", out var headerValues),
            "Expected 'api-supported-versions' header to be present in response headers, but it was missing.");

        var supportedVersions = string.Join(",", headerValues)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var expected in expectedVersions)
        {
            Assert.Contains(
                expected,
                supportedVersions);
        }
    }

    /// <summary>
    /// Asserts that the response contains the <c>api-deprecated-versions</c> header with the specified versions.
    /// </summary>
    public static void ShouldHaveDeprecatedApiVersions(
        this HttpResponseMessage response,
        params string[] expectedVersions)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(expectedVersions);

        Assert.True(
            response.Headers.TryGetValues("api-deprecated-versions", out var headerValues),
            "Expected 'api-deprecated-versions' header to be present in response headers, but it was missing.");

        var deprecatedVersions = string.Join(",", headerValues)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var expected in expectedVersions)
        {
            Assert.Contains(
                expected,
                deprecatedVersions);
        }
    }
}
