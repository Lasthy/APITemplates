using System.Security.Cryptography;
using System.Text;

namespace Kernel.Encryption;

/// <summary>
/// Default implementation of <see cref="IEncryptionService"/> using AES-256-GCM authenticated encryption.
/// Encrypted payloads contain a 12-byte random nonce, 16-byte authentication tag, and the ciphertext.
/// </summary>
public class AesEncryptionService : IEncryptionService
{
    private const int NonceSize = 12; // 96-bit nonce standard for AES-GCM
    private const int TagSize = 16;   // 128-bit authentication tag standard for AES-GCM
    private readonly Func<byte[]> _keyProvider;

    /// <summary>
    /// Initializes a new instance of <see cref="AesEncryptionService"/> using <see cref="EncryptionSettings"/>.
    /// The encryption key is evaluated dynamically from the settings.
    /// </summary>
    public AesEncryptionService(EncryptionSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _keyProvider = () => settings.GetKeyBytes();
    }

    /// <summary>
    /// Initializes a new instance of <see cref="AesEncryptionService"/> with a fixed 32-byte key.
    /// </summary>
    public AesEncryptionService(byte[] key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (key.Length != 32)
        {
            throw new ArgumentException("Encryption key must be exactly 32 bytes (256 bits) for AES-256.", nameof(key));
        }

        var keyCopy = (byte[])key.Clone();
        _keyProvider = () => keyCopy;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="AesEncryptionService"/> with a Base64-encoded key or passphrase.
    /// </summary>
    public AesEncryptionService(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        byte[] keyBytes;
        try
        {
            var bytes = Convert.FromBase64String(key);
            keyBytes = bytes.Length == 32 ? bytes : SHA256.HashData(Encoding.UTF8.GetBytes(key));
        }
        catch
        {
            keyBytes = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        }

        _keyProvider = () => keyBytes;
    }

    /// <inheritdoc />
    public byte[] Encrypt(byte[] plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);

        var key = _keyProvider();
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var tag = new byte[TagSize];
        var ciphertext = new byte[plaintext.Length];

        using (var aesGcm = new AesGcm(key, TagSize))
        {
            aesGcm.Encrypt(nonce, plaintext, ciphertext, tag);
        }

        var result = new byte[NonceSize + TagSize + ciphertext.Length];
        Buffer.BlockCopy(nonce, 0, result, 0, NonceSize);
        Buffer.BlockCopy(tag, 0, result, NonceSize, TagSize);
        Buffer.BlockCopy(ciphertext, 0, result, NonceSize + TagSize, ciphertext.Length);

        return result;
    }

    /// <inheritdoc />
    public byte[] Decrypt(byte[] ciphertext)
    {
        ArgumentNullException.ThrowIfNull(ciphertext);

        if (ciphertext.Length < NonceSize + TagSize)
        {
            throw new CryptographicException("Ciphertext payload is too short to contain valid nonce and authentication tag.");
        }

        var key = _keyProvider();
        var nonce = new byte[NonceSize];
        var tag = new byte[TagSize];
        var cipher = new byte[ciphertext.Length - NonceSize - TagSize];

        Buffer.BlockCopy(ciphertext, 0, nonce, 0, NonceSize);
        Buffer.BlockCopy(ciphertext, NonceSize, tag, 0, TagSize);
        Buffer.BlockCopy(ciphertext, NonceSize + TagSize, cipher, 0, cipher.Length);

        var plaintext = new byte[cipher.Length];
        using (var aesGcm = new AesGcm(key, TagSize))
        {
            aesGcm.Decrypt(nonce, cipher, tag, plaintext);
        }

        return plaintext;
    }

    /// <inheritdoc />
    public string Encrypt(string plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);

        var plainBytes = Encoding.UTF8.GetBytes(plaintext);
        var encryptedBytes = Encrypt(plainBytes);
        return Convert.ToBase64String(encryptedBytes);
    }

    /// <inheritdoc />
    public string Decrypt(string ciphertext)
    {
        ArgumentNullException.ThrowIfNull(ciphertext);

        var cipherBytes = Convert.FromBase64String(ciphertext);
        var decryptedBytes = Decrypt(cipherBytes);
        return Encoding.UTF8.GetString(decryptedBytes);
    }
}
