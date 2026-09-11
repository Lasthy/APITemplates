using System.Security.Cryptography;
using Kernel.Encryption;
using Xunit;

namespace Kernel.Tests.Encryption;

public class EncryptionTests
{
    private static byte[] GenerateKey()
    {
        var key = new byte[32];
        RandomNumberGenerator.Fill(key);
        return key;
    }

    [Fact]
    public void Encrypt_And_Decrypt_String_Roundtrip()
    {
        var key = GenerateKey();
        var service = new AesEncryptionService(key);

        const string plaintext = "SuperSecretPassword123!@#";
        var ciphertext = service.Encrypt(plaintext);

        Assert.NotNull(ciphertext);
        Assert.NotEqual(plaintext, ciphertext);

        var decrypted = service.Decrypt(ciphertext);
        Assert.Equal(plaintext, decrypted);
    }

    [Fact]
    public void Encrypt_And_Decrypt_Bytes_Roundtrip()
    {
        var key = GenerateKey();
        var service = new AesEncryptionService(key);

        var plainBytes = new byte[] { 1, 2, 3, 4, 5, 42, 100 };
        var cipherBytes = service.Encrypt(plainBytes);

        Assert.NotNull(cipherBytes);
        Assert.NotEqual(plainBytes, cipherBytes);

        var decryptedBytes = service.Decrypt(cipherBytes);
        Assert.Equal(plainBytes, decryptedBytes);
    }

    [Fact]
    public void Encrypt_SamePlaintextTwice_ShouldProduceDifferentCiphertexts()
    {
        var key = GenerateKey();
        var service = new AesEncryptionService(key);

        const string plaintext = "ConstantData";
        var cipher1 = service.Encrypt(plaintext);
        var cipher2 = service.Encrypt(plaintext);

        // Due to unique nonces, ciphertexts must differ
        Assert.NotEqual(cipher1, cipher2);

        // But both decrypt back to the original plaintext
        Assert.Equal(plaintext, service.Decrypt(cipher1));
        Assert.Equal(plaintext, service.Decrypt(cipher2));
    }

    [Fact]
    public void Decrypt_TamperedCiphertext_ShouldThrowCryptographicException()
    {
        var key = GenerateKey();
        var service = new AesEncryptionService(key);

        var ciphertext = service.Encrypt("SensitiveData");
        var rawBytes = Convert.FromBase64String(ciphertext);

        // Flip a byte in the tag or ciphertext
        rawBytes[^1] ^= 0xFF;
        var tampered = Convert.ToBase64String(rawBytes);

        Assert.ThrowsAny<CryptographicException>(() => service.Decrypt(tampered));
    }

    [Fact]
    public void InvalidKeyLength_ShouldThrowArgumentException()
    {
        var invalidKey = new byte[16]; // Only 16 bytes instead of 32
        Assert.Throws<ArgumentException>(() => new AesEncryptionService(invalidKey));
    }

    [Fact]
    public void EncryptedValueConverter_ShouldConvertAndRestore()
    {
        var key = GenerateKey();
        var service = new AesEncryptionService(key);
        var converter = new EncryptedValueConverter(service);

        var toProvider = converter.ConvertToProvider;
        var fromProvider = converter.ConvertFromProvider;

        var encrypted = (string)toProvider("Sensitive Column Value")!;
        Assert.NotNull(encrypted);

        var restored = (string)fromProvider(encrypted)!;
        Assert.Equal("Sensitive Column Value", restored);
    }
}
