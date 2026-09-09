using Webhooks.Delivery;
using Xunit;

namespace Webhooks.Tests;

public class HmacSignatureServiceTests
{
    private readonly HmacSignatureService _signer = new();

    [Fact]
    public void Sign_ReturnsValidSha256PrefixedHex()
    {
        var secret = "super-secret-key-12345";
        var timestamp = "1725800000";
        var payload = "{\"event\":\"UserCreated\"}";

        var signature = _signer.Sign(secret, timestamp, payload);

        Assert.NotNull(signature);
        Assert.StartsWith("sha256=", signature);
        Assert.Equal(71, signature.Length); // "sha256=" (7) + 64 hex characters
    }

    [Fact]
    public void Verify_ReturnsTrue_ForExactMatchingSignature()
    {
        var secret = "super-secret-key-12345";
        var timestamp = "1725800000";
        var payload = "{\"event\":\"UserCreated\"}";

        var signature = _signer.Sign(secret, timestamp, payload);
        var isValid = _signer.Verify(secret, timestamp, payload, signature);

        Assert.True(isValid);
    }

    [Fact]
    public void Verify_ReturnsTrue_WithoutSha256Prefix()
    {
        var secret = "super-secret-key-12345";
        var timestamp = "1725800000";
        var payload = "{\"event\":\"UserCreated\"}";

        var signature = _signer.Sign(secret, timestamp, payload);
        var hexOnly = signature.Replace("sha256=", "");

        var isValid = _signer.Verify(secret, timestamp, payload, hexOnly);
        Assert.True(isValid);
    }

    [Fact]
    public void Verify_ReturnsFalse_WhenSecretIsIncorrect()
    {
        var secret = "super-secret-key-12345";
        var wrongSecret = "wrong-secret-key-99999";
        var timestamp = "1725800000";
        var payload = "{\"event\":\"UserCreated\"}";

        var signature = _signer.Sign(secret, timestamp, payload);
        var isValid = _signer.Verify(wrongSecret, timestamp, payload, signature);

        Assert.False(isValid);
    }

    [Fact]
    public void Verify_ReturnsFalse_WhenPayloadIsTampered()
    {
        var secret = "super-secret-key-12345";
        var timestamp = "1725800000";
        var payload = "{\"event\":\"UserCreated\"}";
        var tamperedPayload = "{\"event\":\"UserDeleted\"}";

        var signature = _signer.Sign(secret, timestamp, payload);
        var isValid = _signer.Verify(secret, timestamp, tamperedPayload, signature);

        Assert.False(isValid);
    }

    [Fact]
    public void Verify_ReturnsFalse_WhenTimestampIsModified()
    {
        var secret = "super-secret-key-12345";
        var timestamp = "1725800000";
        var modifiedTimestamp = "1725800001";
        var payload = "{\"event\":\"UserCreated\"}";

        var signature = _signer.Sign(secret, timestamp, payload);
        var isValid = _signer.Verify(secret, modifiedTimestamp, payload, signature);

        Assert.False(isValid);
    }

    [Theory]
    [InlineData("", "1725800000", "payload")]
    [InlineData("   ", "1725800000", "payload")]
    public void Sign_ThrowsOnEmptySecret(string secret, string timestamp, string payload)
    {
        Assert.Throws<ArgumentException>(() => _signer.Sign(secret, timestamp, payload));
    }
}
