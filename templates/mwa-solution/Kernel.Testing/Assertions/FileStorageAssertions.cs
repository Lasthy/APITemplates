using System.Text;
using Kernel.Testing.Fakes;
using Xunit;

namespace Kernel.Testing.Assertions;

/// <summary>
/// Fluent assertion extensions for <see cref="FakeFileStorage"/>.
/// </summary>
public static class FileStorageAssertions
{
    /// <summary>
    /// Asserts that a file exists at the specified storage path.
    /// </summary>
    public static void ShouldHaveFile(
        this FakeFileStorage storage,
        string path,
        string? userMessage = null)
    {
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(path);

        Assert.True(
            storage.ContainsFile(path),
            userMessage ?? $"Expected storage to contain file at '{path}', but it was not found.");
    }

    /// <summary>
    /// Asserts that a file exists at the specified storage path with exact matching byte content.
    /// </summary>
    public static void ShouldHaveFile(
        this FakeFileStorage storage,
        string path,
        byte[] expectedBytes,
        string? userMessage = null)
    {
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(expectedBytes);

        storage.ShouldHaveFile(path, userMessage);

        var actualBytes = storage.GetFileBytes(path);
        Assert.Equal(expectedBytes, actualBytes);
    }

    /// <summary>
    /// Asserts that a file exists at the specified storage path with matching text content.
    /// </summary>
    public static void ShouldHaveFile(
        this FakeFileStorage storage,
        string path,
        string expectedText,
        Encoding? encoding = null,
        string? userMessage = null)
    {
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(expectedText);

        storage.ShouldHaveFile(path, userMessage);

        var actualText = storage.GetFileText(path, encoding);
        Assert.Equal(expectedText, actualText);
    }

    /// <summary>
    /// Asserts that no file exists at the specified storage path.
    /// </summary>
    public static void ShouldNotHaveFile(
        this FakeFileStorage storage,
        string path,
        string? userMessage = null)
    {
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(path);

        Assert.False(
            storage.ContainsFile(path),
            userMessage ?? $"Expected storage not to contain file at '{path}', but it was present.");
    }

    /// <summary>
    /// Asserts that the specified path was recorded as deleted.
    /// </summary>
    public static void ShouldHaveDeleted(
        this FakeFileStorage storage,
        string path,
        string? userMessage = null)
    {
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(path);

        var normalizedPath = path.Replace('\\', '/').TrimStart('/');
        Assert.Contains(
            normalizedPath,
            storage.DeletedPaths);
    }

    /// <summary>
    /// Asserts that the storage is completely empty.
    /// </summary>
    public static void ShouldBeEmpty(
        this FakeFileStorage storage,
        string? userMessage = null)
    {
        ArgumentNullException.ThrowIfNull(storage);

        Assert.True(
            storage.Count == 0,
            userMessage ?? $"Expected storage to be empty, but it contained {storage.Count} file(s).");
    }

    /// <summary>
    /// Asserts that the storage contains exactly <paramref name="expectedCount"/> files.
    /// </summary>
    public static void ShouldHaveCount(
        this FakeFileStorage storage,
        int expectedCount,
        string? userMessage = null)
    {
        ArgumentNullException.ThrowIfNull(storage);

        Assert.True(
            storage.Count == expectedCount,
            userMessage ?? $"Expected storage to have {expectedCount} files, but found {storage.Count}.");
    }
}
