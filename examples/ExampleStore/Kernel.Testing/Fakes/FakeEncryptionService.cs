using System.Text;
using Kernel.Encryption;

namespace Kernel.Testing.Fakes;

/// <summary>
/// A test fake implementing <see cref="IEncryptionService"/> that records all encryption and decryption operations.
/// Supports transparent prefix transformation (default) or real AES-GCM encryption, as well as simulated failures.
/// </summary>
public class FakeEncryptionService : IEncryptionService
{
    private readonly List<(string Plaintext, string Ciphertext)> _encryptedStrings = [];
    private readonly List<(string Ciphertext, string Plaintext)> _decryptedStrings = [];
    private readonly List<(byte[] Plaintext, byte[] Ciphertext)> _encryptedBytes = [];
    private readonly List<(byte[] Ciphertext, byte[] Plaintext)> _decryptedBytes = [];
    private readonly object _lock = new();

    private AesEncryptionService? _realEncryptionService;

    /// <summary>
    /// The prefix used in fake string encryption when <see cref="UseRealEncryption"/> is false. Defaults to <c>"[ENC]"</c>.
    /// </summary>
    public string Prefix { get; set; } = "[ENC]";

    /// <summary>
    /// Whether to use real AES-256-GCM encryption instead of prefix-based transforms. Defaults to <c>false</c>.
    /// </summary>
    public bool UseRealEncryption { get; set; } = false;

    /// <summary>
    /// Controls whether <see cref="Encrypt(string)"/> and <see cref="Encrypt(byte[])"/> should throw.
    /// </summary>
    public bool ShouldThrowOnEncrypt { get; set; } = false;

    /// <summary>
    /// Custom exception to throw when <see cref="ShouldThrowOnEncrypt"/> is true.
    /// </summary>
    public Exception? EncryptException { get; set; }

    /// <summary>
    /// Controls whether <see cref="Decrypt(string)"/> and <see cref="Decrypt(byte[])"/> should throw.
    /// </summary>
    public bool ShouldThrowOnDecrypt { get; set; } = false;

    /// <summary>
    /// Custom exception to throw when <see cref="ShouldThrowOnDecrypt"/> is true.
    /// </summary>
    public Exception? DecryptException { get; set; }

    /// <summary>
    /// Gets all recorded string encryptions as (Plaintext, Ciphertext) pairs.
    /// </summary>
    public IReadOnlyList<(string Plaintext, string Ciphertext)> EncryptedStrings
    {
        get
        {
            lock (_lock)
            {
                return _encryptedStrings.ToList();
            }
        }
    }

    /// <summary>
    /// Gets all recorded string decryptions as (Ciphertext, Plaintext) pairs.
    /// </summary>
    public IReadOnlyList<(string Ciphertext, string Plaintext)> DecryptedStrings
    {
        get
        {
            lock (_lock)
            {
                return _decryptedStrings.ToList();
            }
        }
    }

    /// <summary>
    /// Gets the total number of encryption operations performed (strings + byte arrays).
    /// </summary>
    public int TotalEncryptions
    {
        get
        {
            lock (_lock)
            {
                return _encryptedStrings.Count + _encryptedBytes.Count;
            }
        }
    }

    /// <summary>
    /// Gets the total number of decryption operations performed (strings + byte arrays).
    /// </summary>
    public int TotalDecryptions
    {
        get
        {
            lock (_lock)
            {
                return _decryptedStrings.Count + _decryptedBytes.Count;
            }
        }
    }

    public string Encrypt(string plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);

        if (ShouldThrowOnEncrypt)
        {
            throw EncryptException ?? new InvalidOperationException("Simulated encryption failure.");
        }

        string ciphertext;
        if (UseRealEncryption)
        {
            _realEncryptionService ??= new AesEncryptionService("MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=");
            ciphertext = _realEncryptionService.Encrypt(plaintext);
        }
        else
        {
            ciphertext = $"{Prefix}{plaintext}";
        }

        lock (_lock)
        {
            _encryptedStrings.Add((plaintext, ciphertext));
        }

        return ciphertext;
    }

    public string Decrypt(string ciphertext)
    {
        ArgumentNullException.ThrowIfNull(ciphertext);

        if (ShouldThrowOnDecrypt)
        {
            throw DecryptException ?? new InvalidOperationException("Simulated decryption failure.");
        }

        string plaintext;
        if (UseRealEncryption)
        {
            _realEncryptionService ??= new AesEncryptionService("MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=");
            plaintext = _realEncryptionService.Decrypt(ciphertext);
        }
        else
        {
            plaintext = ciphertext.StartsWith(Prefix, StringComparison.Ordinal)
                ? ciphertext[Prefix.Length..]
                : ciphertext;
        }

        lock (_lock)
        {
            _decryptedStrings.Add((ciphertext, plaintext));
        }

        return plaintext;
    }

    public byte[] Encrypt(byte[] plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);

        if (ShouldThrowOnEncrypt)
        {
            throw EncryptException ?? new InvalidOperationException("Simulated encryption failure.");
        }

        byte[] ciphertext;
        if (UseRealEncryption)
        {
            _realEncryptionService ??= new AesEncryptionService("MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=");
            ciphertext = _realEncryptionService.Encrypt(plaintext);
        }
        else
        {
            var prefixBytes = Encoding.UTF8.GetBytes(Prefix);
            ciphertext = new byte[prefixBytes.Length + plaintext.Length];
            Buffer.BlockCopy(prefixBytes, 0, ciphertext, 0, prefixBytes.Length);
            Buffer.BlockCopy(plaintext, 0, ciphertext, prefixBytes.Length, plaintext.Length);
        }

        lock (_lock)
        {
            _encryptedBytes.Add((plaintext, ciphertext));
        }

        return ciphertext;
    }

    public byte[] Decrypt(byte[] ciphertext)
    {
        ArgumentNullException.ThrowIfNull(ciphertext);

        if (ShouldThrowOnDecrypt)
        {
            throw DecryptException ?? new InvalidOperationException("Simulated decryption failure.");
        }

        byte[] plaintext;
        if (UseRealEncryption)
        {
            _realEncryptionService ??= new AesEncryptionService("MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=");
            plaintext = _realEncryptionService.Decrypt(ciphertext);
        }
        else
        {
            var prefixBytes = Encoding.UTF8.GetBytes(Prefix);
            if (ciphertext.Length >= prefixBytes.Length &&
                ciphertext.Take(prefixBytes.Length).SequenceEqual(prefixBytes))
            {
                plaintext = new byte[ciphertext.Length - prefixBytes.Length];
                Buffer.BlockCopy(ciphertext, prefixBytes.Length, plaintext, 0, plaintext.Length);
            }
            else
            {
                plaintext = (byte[])ciphertext.Clone();
            }
        }

        lock (_lock)
        {
            _decryptedBytes.Add((ciphertext, plaintext));
        }

        return plaintext;
    }

    /// <summary>
    /// Checks whether the specified plaintext string was encrypted by this fake.
    /// </summary>
    public bool HasEncrypted(string plaintext)
    {
        lock (_lock)
        {
            return _encryptedStrings.Any(x => x.Plaintext == plaintext);
        }
    }

    /// <summary>
    /// Checks whether the specified ciphertext string was decrypted by this fake.
    /// </summary>
    public bool HasDecrypted(string ciphertext)
    {
        lock (_lock)
        {
            return _decryptedStrings.Any(x => x.Ciphertext == ciphertext);
        }
    }

    /// <summary>
    /// Resets all recorded operations and configuration flags.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _encryptedStrings.Clear();
            _decryptedStrings.Clear();
            _encryptedBytes.Clear();
            _decryptedBytes.Clear();
            ShouldThrowOnEncrypt = false;
            EncryptException = null;
            ShouldThrowOnDecrypt = false;
            DecryptException = null;
            UseRealEncryption = false;
        }
    }
}
