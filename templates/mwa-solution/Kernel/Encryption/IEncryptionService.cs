namespace Kernel.Encryption;

/// <summary>
/// Abstraction for encrypting and decrypting sensitive data at rest.
/// </summary>
public interface IEncryptionService
{
    /// <summary>
    /// Encrypts a plaintext string into a Base64-encoded ciphertext string.
    /// </summary>
    /// <param name="plaintext">The plaintext string to encrypt.</param>
    /// <returns>A Base64-encoded string containing the encrypted payload (nonce + authentication tag + ciphertext).</returns>
    string Encrypt(string plaintext);

    /// <summary>
    /// Decrypts a Base64-encoded ciphertext string into the original plaintext.
    /// </summary>
    /// <param name="ciphertext">The Base64-encoded ciphertext string.</param>
    /// <returns>The decrypted plaintext string.</returns>
    string Decrypt(string ciphertext);

    /// <summary>
    /// Encrypts a plaintext byte array into a ciphertext byte array.
    /// </summary>
    /// <param name="plaintext">The plaintext bytes to encrypt.</param>
    /// <returns>A byte array containing the encrypted payload (nonce + authentication tag + ciphertext).</returns>
    byte[] Encrypt(byte[] plaintext);

    /// <summary>
    /// Decrypts a ciphertext byte array into the original plaintext bytes.
    /// </summary>
    /// <param name="ciphertext">The ciphertext bytes (nonce + authentication tag + ciphertext).</param>
    /// <returns>The decrypted plaintext bytes.</returns>
    byte[] Decrypt(byte[] ciphertext);
}
