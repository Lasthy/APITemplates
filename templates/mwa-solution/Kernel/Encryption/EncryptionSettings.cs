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
    /// <summary>
    /// Default hardcoded base64 key intended for development and testing environments only.
    /// </summary>
    public const string DefaultBase64Key = "MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=";

    /// <inheritdoc />
    public override string SectionName => "Encryption";

    /// <summary>
    /// Whether the currently configured key matches the default development key or is empty.
    /// </summary>
    public bool IsDefaultKey =>
        !TryGetValue<string>(nameof(Key), out var val) ||
        string.IsNullOrWhiteSpace(val) ||
        string.Equals(val, DefaultBase64Key, StringComparison.Ordinal);

    /// <summary>
    /// The 256-bit secret encryption key. Can be provided as a Base64-encoded 32-byte string
    /// or as a passphrase (which will be derived via SHA-256).
    /// </summary>
    [Sensitive(AllowApiMutation = false)]
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
        if (IsDefaultKey)
        {
            var env = EnvironmentName ??
                      System.Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ??
                      System.Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ??
                      "Production";

            var isDevelopment = string.Equals(env, "Development", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(env, "Test", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(env, "Testing", StringComparison.OrdinalIgnoreCase);

            if (!isDevelopment)
            {
                throw new InvalidOperationException(
                    "In production environments, a secure, non-default 256-bit AES encryption key must be configured in appsettings.Encryption.json or via the ENCRYPTION__KEY environment variable.");
            }
        }

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
