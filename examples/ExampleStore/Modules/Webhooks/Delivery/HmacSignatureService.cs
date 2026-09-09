using System.Security.Cryptography;
using System.Text;

namespace Webhooks.Delivery;

/// <summary>
/// HMAC-SHA256 implementation of <see cref="ISignatureService"/>.
/// Generates and verifies signatures of the format <c>sha256={hex}</c> over <c>{timestamp}.{payload}</c>.
/// </summary>
public class HmacSignatureService : ISignatureService
{
    private const string Prefix = "sha256=";

    public string Sign(string secret, string timestamp, string payload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);
        ArgumentNullException.ThrowIfNull(timestamp);
        ArgumentNullException.ThrowIfNull(payload);

        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var messageBytes = Encoding.UTF8.GetBytes($"{timestamp}.{payload}");

        using var hmac = new HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(messageBytes);
        var hex = Convert.ToHexString(hash).ToLowerInvariant();

        return $"{Prefix}{hex}";
    }

    public bool Verify(string secret, string timestamp, string payload, string signature)
    {
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(signature))
        {
            return false;
        }

        var expectedSignature = Sign(secret, timestamp, payload);

        var normalizedProvided = signature.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)
            ? signature
            : $"{Prefix}{signature}";

        var providedBytes = Encoding.UTF8.GetBytes(normalizedProvided.ToLowerInvariant());
        var expectedBytes = Encoding.UTF8.GetBytes(expectedSignature.ToLowerInvariant());

        return CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes);
    }
}
