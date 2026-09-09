namespace Webhooks.Delivery;

/// <summary>
/// Abstraction for signing and verifying outbound webhook payloads.
/// </summary>
public interface ISignatureService
{
    /// <summary>
    /// Computes the HMAC signature for the given payload and timestamp using the secret.
    /// </summary>
    /// <param name="secret">The shared secret key.</param>
    /// <param name="timestamp">The Unix epoch timestamp in seconds.</param>
    /// <param name="payload">The raw JSON payload string.</param>
    /// <returns>Formatted signature string (e.g. <c>sha256={hex}</c>).</returns>
    string Sign(string secret, string timestamp, string payload);

    /// <summary>
    /// Verifies the provided signature against the computed HMAC.
    /// </summary>
    /// <param name="secret">The shared secret key.</param>
    /// <param name="timestamp">The Unix epoch timestamp in seconds.</param>
    /// <param name="payload">The raw JSON payload string.</param>
    /// <param name="signature">The signature header value to verify.</param>
    /// <returns>True if signature matches; otherwise false.</returns>
    bool Verify(string secret, string timestamp, string payload, string signature);
}
