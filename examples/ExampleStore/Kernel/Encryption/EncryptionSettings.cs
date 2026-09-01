using System.Security.Cryptography;
using System.Text;
using Kernel.Configuration;
using Kernel.Json;

namespace Kernel.Encryption;

/// <summary>
/// Dynamic settings for data encryption, loaded from <c>appsettings.Encryption.json</c>
/// with optional environment overlay <c>appsettings.Encryption.{Environment}.json</c>.
/// </summary>
public class EncryptionSettings : DynamicSettingsSection
{
    private const string DefaultBase64Key = "MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=";

    /// <inheritdoc />
    public override string SectionName => "Encryption";

    /// <summary>
    /// The 256-bit secret encryption key. Can be provided as a Base64-encoded 32-byte string
    /// or as a passphrase (which will be derived via SHA-256).
    /// </summary>
    public string Key =>
        TryGetValue<string>(nameof(Key), out var val) && !string.IsNullOrWhiteSpace(val) ? val : DefaultBase64Key;

    public EncryptionSettings(IJsonSerializer jsonSerializer, string? environment = null)
        : base("appsettings.Encryption.json", jsonSerializer, environment)
    {
    }

    public EncryptionSettings(string source, IJsonSerializer jsonSerializer, string? environment = null)
        : base(source, jsonSerializer, environment)
    {
    }

    /// <summary>
    /// Resolves the 32-byte (256-bit) encryption key bytes from the configured <see cref="Key"/>.
    /// If the key is valid Base64 decoding to 32 bytes, those bytes are used.
    /// Otherwise, a 32-byte key is derived from the key string via SHA-256.
    /// </summary>
    public byte[] GetKeyBytes()
    {
        var rawKey = Key;
        if (string.IsNullOrWhiteSpace(rawKey))
        {
            rawKey = DefaultBase64Key;
        }

        try
        {
            var bytes = Convert.FromBase64String(rawKey);
            if (bytes.Length == 32)
            {
                return bytes;
            }
        }
        catch
        {
            // Not a valid Base64 string, proceed to SHA256 derivation
        }

        return SHA256.HashData(Encoding.UTF8.GetBytes(rawKey));
    }
}
