using Kernel.Testing.Fakes;
using Xunit;

namespace Kernel.Testing.Assertions;

/// <summary>
/// Fluent assertion extensions for <see cref="FakeEncryptionService"/>.
/// </summary>
public static class EncryptionAssertions
{
    /// <summary>
    /// Asserts that the fake encryption service encrypted the specified plaintext string.
    /// </summary>
    public static void ShouldHaveEncrypted(
        this FakeEncryptionService fake,
        string plaintext,
        string? userMessage = null)
    {
        ArgumentNullException.ThrowIfNull(fake);
        ArgumentNullException.ThrowIfNull(plaintext);

        Assert.True(
            fake.HasEncrypted(plaintext),
            userMessage ?? $"Expected encryption of plaintext '{plaintext}', but it was not encrypted.");
    }

    /// <summary>
    /// Asserts that the fake encryption service decrypted the specified ciphertext string.
    /// </summary>
    public static void ShouldHaveDecrypted(
        this FakeEncryptionService fake,
        string ciphertext,
        string? userMessage = null)
    {
        ArgumentNullException.ThrowIfNull(fake);
        ArgumentNullException.ThrowIfNull(ciphertext);

        Assert.True(
            fake.HasDecrypted(ciphertext),
            userMessage ?? $"Expected decryption of ciphertext '{ciphertext}', but it was not decrypted.");
    }

    /// <summary>
    /// Asserts that the total number of encryption operations matches the expected count.
    /// </summary>
    public static void ShouldHaveEncryptedCount(
        this FakeEncryptionService fake,
        int expectedCount,
        string? userMessage = null)
    {
        ArgumentNullException.ThrowIfNull(fake);

        Assert.True(
            fake.TotalEncryptions == expectedCount,
            userMessage ?? $"Expected {expectedCount} encryption(s), but found {fake.TotalEncryptions}.");
    }

    /// <summary>
    /// Asserts that the total number of decryption operations matches the expected count.
    /// </summary>
    public static void ShouldHaveDecryptedCount(
        this FakeEncryptionService fake,
        int expectedCount,
        string? userMessage = null)
    {
        ArgumentNullException.ThrowIfNull(fake);

        Assert.True(
            fake.TotalDecryptions == expectedCount,
            userMessage ?? $"Expected {expectedCount} decryption(s), but found {fake.TotalDecryptions}.");
    }

    /// <summary>
    /// Asserts that no encryption or decryption operations have been performed.
    /// </summary>
    public static void ShouldBeEmpty(
        this FakeEncryptionService fake,
        string? userMessage = null)
    {
        ArgumentNullException.ThrowIfNull(fake);

        Assert.True(
            fake.TotalEncryptions == 0 && fake.TotalDecryptions == 0,
            userMessage ?? $"Expected fake encryption service to be empty, but it had {fake.TotalEncryptions} encryptions and {fake.TotalDecryptions} decryptions.");
    }
}
