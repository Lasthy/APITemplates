using System.Security.Cryptography;
using System.Text;
using Kernel.Configuration;
using Kernel.Encryption;
using Kernel.Encryption.Extensions;
using Kernel.Json;
using Kernel.Persistence;
using Kernel.Persistence.Extensions;
using Kernel.Testing.Assertions;
using Kernel.Testing.Fakes;
using Kernel.Testing.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Sdk;

namespace Shopping.Tests;

public class EncryptionTests
{
    #region Test Entities and DbContext

    public class TestEncryptedEntity
    {
        public DefaultId Id { get; set; } = DefaultId.NewUlid();

        [Encrypted]
        public string SecretToken { get; set; } = null!;

        [Encrypted]
        public string? OptionalSecret { get; set; }

        [Encrypted]
        public byte[]? BinaryKey { get; set; }

        public string PlainData { get; set; } = null!;
    }

    public class InvalidEncryptedEntity
    {
        public DefaultId Id { get; set; } = DefaultId.NewUlid();

        [Encrypted]
        public int UnsupportedTypeProperty { get; set; }
    }

    public class TestEncryptionDbContext : DbContext, IDbContextSchema
    {
        public string Schema => "test";
        private readonly IEncryptionService? _encryptionService;

        public DbSet<TestEncryptedEntity> Entities { get; set; } = null!;

        public TestEncryptionDbContext(
            DbContextOptions<TestEncryptionDbContext> options,
            IEncryptionService? encryptionService = null)
            : base(options)
        {
            _encryptionService = encryptionService;
        }

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            base.ConfigureConventions(configurationBuilder);
            configurationBuilder.Properties<DefaultId>().HaveConversion<Cysharp.EntityFrameworkCore.UlidToStringConverter>();
            configurationBuilder.Properties<DefaultId?>().HaveConversion<Cysharp.EntityFrameworkCore.UlidToNullableStringConverter>();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplySchema(Schema, Database);

            modelBuilder.Entity<TestEncryptedEntity>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasMaxLength(26);
                entity.Property(e => e.PlainData).HasMaxLength(100);
            });

            if (_encryptionService != null)
            {
                modelBuilder.UseEncryption(_encryptionService);
            }
            else
            {
                modelBuilder.UseEncryption(this);
            }
        }
    }

    public class InvalidEncryptionDbContext : DbContext
    {
        private readonly IEncryptionService _encryptionService;

        public DbSet<InvalidEncryptedEntity> InvalidEntities { get; set; } = null!;

        public InvalidEncryptionDbContext(
            DbContextOptions<InvalidEncryptionDbContext> options,
            IEncryptionService encryptionService)
            : base(options)
        {
            _encryptionService = encryptionService;
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.UseEncryption(_encryptionService);
        }
    }

    public class RawTestEncryptionDbContext : DbContext
    {
        public DbSet<TestEncryptedEntity> Entities { get; set; } = null!;

        public RawTestEncryptionDbContext(DbContextOptions<RawTestEncryptionDbContext> options)
            : base(options)
        {
        }

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            base.ConfigureConventions(configurationBuilder);
            configurationBuilder.Properties<DefaultId>().HaveConversion<Cysharp.EntityFrameworkCore.UlidToStringConverter>();
            configurationBuilder.Properties<DefaultId?>().HaveConversion<Cysharp.EntityFrameworkCore.UlidToNullableStringConverter>();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<TestEncryptedEntity>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasMaxLength(26);
                entity.Property(e => e.PlainData).HasMaxLength(100);
            });
        }
    }

    #endregion

    #region AesEncryptionService Unit Tests

    [Fact]
    public void Encrypt_And_Decrypt_String_ReturnsOriginalPlaintext()
    {
        var service = new AesEncryptionService("MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=");
        var plaintext = "This is super sensitive user data: API_KEY_9876543210";

        var ciphertext = service.Encrypt(plaintext);
        var decrypted = service.Decrypt(ciphertext);

        Assert.NotNull(ciphertext);
        Assert.NotEqual(plaintext, ciphertext);
        Assert.Equal(plaintext, decrypted);
    }

    [Fact]
    public void Encrypt_And_Decrypt_EmptyString_ReturnsEmptyString()
    {
        var service = new AesEncryptionService("MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=");
        var plaintext = string.Empty;

        var ciphertext = service.Encrypt(plaintext);
        var decrypted = service.Decrypt(ciphertext);

        Assert.NotEmpty(ciphertext); // Nonce + Tag + 0 bytes ciphertext is 28 bytes = ~38 Base64 chars
        Assert.Equal(string.Empty, decrypted);
    }

    [Fact]
    public void Encrypt_And_Decrypt_LongString_ReturnsOriginalPlaintext()
    {
        var service = new AesEncryptionService("MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=");
        var plaintext = string.Concat(Enumerable.Repeat("The quick brown fox jumps over the lazy dog! 1234567890. ", 100));

        var ciphertext = service.Encrypt(plaintext);
        var decrypted = service.Decrypt(ciphertext);

        Assert.Equal(plaintext, decrypted);
    }

    [Fact]
    public void Encrypt_And_Decrypt_UnicodeAndSpecialCharacters_ReturnsOriginalPlaintext()
    {
        var service = new AesEncryptionService("MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=");
        var plaintext = "🔒 Unicode test: 漢字, 한국어, العربية, Русский, 🚀, Special: \"'\\/\n\r\t!@#$%^&*()_+";

        var ciphertext = service.Encrypt(plaintext);
        var decrypted = service.Decrypt(ciphertext);

        Assert.Equal(plaintext, decrypted);
    }

    [Fact]
    public void Encrypt_And_Decrypt_Bytes_ReturnsOriginalPlaintextBytes()
    {
        var service = new AesEncryptionService("MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=");
        var plaintextBytes = RandomNumberGenerator.GetBytes(64);

        var ciphertextBytes = service.Encrypt(plaintextBytes);
        var decryptedBytes = service.Decrypt(ciphertextBytes);

        Assert.NotEqual(plaintextBytes, ciphertextBytes);
        Assert.Equal(28 + plaintextBytes.Length, ciphertextBytes.Length);
        Assert.Equal(plaintextBytes, decryptedBytes);
    }

    [Fact]
    public void Encrypt_And_Decrypt_EmptyBytes_ReturnsEmptyBytes()
    {
        var service = new AesEncryptionService("MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=");
        var plaintextBytes = Array.Empty<byte>();

        var ciphertextBytes = service.Encrypt(plaintextBytes);
        var decryptedBytes = service.Decrypt(ciphertextBytes);

        Assert.Equal(28, ciphertextBytes.Length);
        Assert.Empty(decryptedBytes);
    }

    [Fact]
    public void Encrypt_SamePlaintext_ProducesDifferentCiphertexts_DueToRandomNonce()
    {
        var service = new AesEncryptionService("MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=");
        var plaintext = "SecretPassword123!";

        var ciphertext1 = service.Encrypt(plaintext);
        var ciphertext2 = service.Encrypt(plaintext);
        var ciphertext3 = service.Encrypt(plaintext);

        Assert.NotEqual(ciphertext1, ciphertext2);
        Assert.NotEqual(ciphertext2, ciphertext3);
        Assert.NotEqual(ciphertext1, ciphertext3);

        Assert.Equal(plaintext, service.Decrypt(ciphertext1));
        Assert.Equal(plaintext, service.Decrypt(ciphertext2));
        Assert.Equal(plaintext, service.Decrypt(ciphertext3));
    }

    [Fact]
    public void Decrypt_TamperedCiphertext_ThrowsCryptographicException()
    {
        var service = new AesEncryptionService("MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=");
        var plaintext = "Highly confidential bank account details";

        var ciphertextBytes = service.Encrypt(Encoding.UTF8.GetBytes(plaintext));
        // Tamper with ciphertext byte (payload starts at index 28)
        ciphertextBytes[^1] ^= 0xFF;

        Assert.ThrowsAny<CryptographicException>(() => service.Decrypt(ciphertextBytes));
    }

    [Fact]
    public void Decrypt_TamperedAuthenticationTag_ThrowsCryptographicException()
    {
        var service = new AesEncryptionService("MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=");
        var plaintext = "Sensitive data";

        var ciphertextBytes = service.Encrypt(Encoding.UTF8.GetBytes(plaintext));
        // Tamper with tag byte (tag is at bytes 12..27)
        ciphertextBytes[15] ^= 0x01;

        Assert.ThrowsAny<CryptographicException>(() => service.Decrypt(ciphertextBytes));
    }

    [Fact]
    public void Decrypt_TooShortCiphertext_ThrowsCryptographicException()
    {
        var service = new AesEncryptionService("MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=");
        var shortCiphertext = new byte[27]; // Minimum is 28 (12 nonce + 16 tag)

        Assert.Throws<CryptographicException>(() => service.Decrypt(shortCiphertext));
    }

    [Fact]
    public void Decrypt_WithDifferentKey_ThrowsCryptographicException()
    {
        var serviceKey1 = new AesEncryptionService("MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=");
        var serviceKey2 = new AesEncryptionService("OTg3NjU0MzIxMGFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=");

        var ciphertext = serviceKey1.Encrypt("ConfidentialMessage");

        Assert.ThrowsAny<CryptographicException>(() => serviceKey2.Decrypt(ciphertext));
    }

    [Fact]
    public void Encrypt_And_Decrypt_NullArguments_ThrowsArgumentNullException()
    {
        var service = new AesEncryptionService("MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=");

        Assert.Throws<ArgumentNullException>(() => service.Encrypt((string)null!));
        Assert.Throws<ArgumentNullException>(() => service.Decrypt((string)null!));
        Assert.Throws<ArgumentNullException>(() => service.Encrypt((byte[])null!));
        Assert.Throws<ArgumentNullException>(() => service.Decrypt((byte[])null!));
    }

    [Fact]
    public void Constructor_Fixed32ByteKey_EncryptsAndDecryptsSuccessfully()
    {
        var rawKey = new byte[32];
        RandomNumberGenerator.Fill(rawKey);

        var service = new AesEncryptionService(rawKey);
        var plaintext = "Testing with 32-byte array constructor";

        var cipher = service.Encrypt(plaintext);
        var decrypted = service.Decrypt(cipher);

        Assert.Equal(plaintext, decrypted);
    }

    [Fact]
    public void Constructor_InvalidKeyLength_ThrowsArgumentException()
    {
        var invalidKey = new byte[16]; // 16 bytes is not 32 bytes
        Assert.Throws<ArgumentException>(() => new AesEncryptionService(invalidKey));
    }

    [Fact]
    public void Constructor_PassphraseString_DerivesKeyViaSha256()
    {
        var service = new AesEncryptionService("MySuperSecretPassphrase123!");
        var plaintext = "Data encrypted with passphrase key";

        var cipher = service.Encrypt(plaintext);
        var decrypted = service.Decrypt(cipher);

        Assert.Equal(plaintext, decrypted);
    }

    #endregion

    #region EncryptionSettings Tests

    [Fact]
    public void EncryptionSettings_DefaultKey_DecodesTo32Bytes()
    {
        var serializer = new JsonSerializer();
        var settings = new EncryptionSettings(serializer, "Testing");

        var keyBytes = settings.GetKeyBytes();

        Assert.NotNull(keyBytes);
        Assert.Equal(32, keyBytes.Length);
    }

    [Fact]
    public void EncryptionSettings_WithPassphrase_DerivesSha256Key()
    {
        var serializer = new JsonSerializer();
        var settings = new EncryptionSettings(serializer, "Testing") { AutoSave = false };
        settings.SetValue("Key", "MyCustomPassword");

        var keyBytes = settings.GetKeyBytes();
        var expectedBytes = SHA256.HashData(Encoding.UTF8.GetBytes("MyCustomPassword"));

        Assert.Equal(expectedBytes, keyBytes);
    }

    [Fact]
    public void EncryptionSettings_DynamicMutation_AesEncryptionServiceReflectsNewKey()
    {
        var serializer = new JsonSerializer();
        var settings = new EncryptionSettings(serializer, "Testing") { AutoSave = false };
        settings.SetValue("Key", "InitialKey123");

        var service = new AesEncryptionService(settings);
        var plaintext = "Dynamic settings test";

        var ciphertext1 = service.Encrypt(plaintext);
        Assert.Equal(plaintext, service.Decrypt(ciphertext1));

        // Change key dynamically
        settings.SetValue("Key", "NewKey456");

        // Old ciphertext should fail with new key
        Assert.ThrowsAny<CryptographicException>(() => service.Decrypt(ciphertext1));

        // New encryption should succeed with new key
        var ciphertext2 = service.Encrypt(plaintext);
        Assert.Equal(plaintext, service.Decrypt(ciphertext2));
    }

    #endregion

    #region EF Core ValueConverter & ModelBuilder Integration Tests

    [Fact]
    public async Task EFCore_EncryptedProperty_IsStoredAsCiphertextInDatabase_AndDecryptedOnRead()
    {
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var encryptionService = new AesEncryptionService("MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=");

        var options = new DbContextOptionsBuilder<TestEncryptionDbContext>()
            .UseSqlite(connection)
            .Options;

        var entityId = DefaultId.NewUlid();
        var secretText = "SuperSecretApiKey_123456789";
        var optionalSecret = "OptionalSecretValue";
        var binaryKey = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08 };
        var plainText = "Public plain data";

        // 1. Create table and save entity with encryption
        using (var db = new TestEncryptionDbContext(options, encryptionService))
        {
            await db.Database.EnsureCreatedAsync();

            var entity = new TestEncryptedEntity
            {
                Id = entityId,
                SecretToken = secretText,
                OptionalSecret = optionalSecret,
                BinaryKey = binaryKey,
                PlainData = plainText
            };
            db.Entities.Add(entity);
            await db.SaveChangesAsync();
        }

        // 2. Read raw row from SQLite database directly via ADO.NET SQL command
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT SecretToken, OptionalSecret, BinaryKey, PlainData FROM Entities WHERE Id = @id";
            var param = cmd.CreateParameter();
            param.ParameterName = "@id";
            param.Value = entityId.ToString();
            cmd.Parameters.Add(param);

            using var reader = await cmd.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());

            var rawSecretToken = reader.GetString(0);
            var rawOptionalSecret = reader.GetString(1);
            var rawBinaryKey = (byte[])reader.GetValue(2);
            var rawPlainData = reader.GetString(3);

            // Raw secret in SQL database column is ciphertext (Base64 string) - NOT plaintext!
            Assert.NotEqual(secretText, rawSecretToken);
            Assert.DoesNotContain("SuperSecretApiKey", rawSecretToken);
            Assert.Equal(secretText, encryptionService.Decrypt(rawSecretToken));

            // Optional secret is also encrypted
            Assert.NotEqual(optionalSecret, rawOptionalSecret);
            Assert.Equal(optionalSecret, encryptionService.Decrypt(rawOptionalSecret));

            // Binary key is encrypted bytes
            Assert.NotEqual(binaryKey, rawBinaryKey);
            Assert.Equal(binaryKey, encryptionService.Decrypt(rawBinaryKey));

            // PlainData was not marked with [Encrypted], so it remains plain in database
            Assert.Equal(plainText, rawPlainData);
        }

        // 3. Read entity through EF Core with TestEncryptionDbContext - transparently decrypted
        using (var db = new TestEncryptionDbContext(options, encryptionService))
        {
            var loaded = await db.Entities.FindAsync(entityId);
            Assert.NotNull(loaded);
            Assert.Equal(secretText, loaded.SecretToken);
            Assert.Equal(optionalSecret, loaded.OptionalSecret);
            Assert.Equal(binaryKey, loaded.BinaryKey);
            Assert.Equal(plainText, loaded.PlainData);
        }

        // 4. Update encrypted property
        using (var db = new TestEncryptionDbContext(options, encryptionService))
        {
            var loaded = await db.Entities.FindAsync(entityId);
            Assert.NotNull(loaded);
            loaded.SecretToken = "UpdatedSecretToken_999";
            loaded.OptionalSecret = null;
            await db.SaveChangesAsync();
        }

        // 5. Verify raw updated value in SQLite
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT SecretToken, OptionalSecret FROM Entities WHERE Id = @id";
            var param = cmd.CreateParameter();
            param.ParameterName = "@id";
            param.Value = entityId.ToString();
            cmd.Parameters.Add(param);

            using var reader = await cmd.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());

            var rawSecretToken = reader.GetString(0);
            Assert.NotEqual("UpdatedSecretToken_999", rawSecretToken);
            Assert.Equal("UpdatedSecretToken_999", encryptionService.Decrypt(rawSecretToken));
            Assert.True(reader.IsDBNull(1));
        }

        // 6. Verify read through EF Core after update
        using (var db = new TestEncryptionDbContext(options, encryptionService))
        {
            var loaded = await db.Entities.FindAsync(entityId);
            Assert.NotNull(loaded);
            Assert.Equal("UpdatedSecretToken_999", loaded.SecretToken);
            Assert.Null(loaded.OptionalSecret);
        }
    }

    [Fact]
    public void ModelBuilder_UnsupportedPropertyType_ThrowsInvalidOperationException()
    {
        var encryptionService = new AesEncryptionService("MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=");
        var options = new DbContextOptionsBuilder<InvalidEncryptionDbContext>()
            .UseInMemoryDatabase($"InvalidDb_{Guid.NewGuid():N}")
            .Options;

        var ex = Assert.Throws<InvalidOperationException>(() =>
        {
            using var db = new InvalidEncryptionDbContext(options, encryptionService);
            var model = db.Model; // Triggers OnModelCreating
        });

        Assert.Contains("UnsupportedTypeProperty", ex.Message);
        Assert.Contains("Int32", ex.Message);
    }

    #endregion

    #region Kernel.Testing Fake and Assertion Tests

    [Fact]
    public void FakeEncryptionService_DefaultPrefix_TransformsAndRecordsOperations()
    {
        var fake = new FakeEncryptionService();
        fake.ShouldBeEmpty();

        var plain = "MySecretData";
        var encrypted = fake.Encrypt(plain);

        Assert.Equal("[ENC]MySecretData", encrypted);
        Assert.Equal(1, fake.TotalEncryptions);
        Assert.True(fake.HasEncrypted(plain));
        fake.ShouldHaveEncrypted(plain);
        fake.ShouldHaveEncryptedCount(1);

        var decrypted = fake.Decrypt(encrypted);
        Assert.Equal(plain, decrypted);
        Assert.Equal(1, fake.TotalDecryptions);
        Assert.True(fake.HasDecrypted(encrypted));
        fake.ShouldHaveDecrypted(encrypted);
        fake.ShouldHaveDecryptedCount(1);

        // Binary
        var bytes = Encoding.UTF8.GetBytes("BinaryTest");
        var encBytes = fake.Encrypt(bytes);
        var decBytes = fake.Decrypt(encBytes);

        Assert.Equal(bytes, decBytes);
        Assert.Equal(2, fake.TotalEncryptions);
        Assert.Equal(2, fake.TotalDecryptions);

        fake.Reset();
        fake.ShouldBeEmpty();
    }

    [Fact]
    public void FakeEncryptionService_RealEncryptionMode_WorksAsAes()
    {
        var fake = new FakeEncryptionService { UseRealEncryption = true };
        var plain = "RealAesWithFake";

        var encrypted = fake.Encrypt(plain);
        Assert.NotEqual(plain, encrypted);
        Assert.False(encrypted.StartsWith("[ENC]"));

        var decrypted = fake.Decrypt(encrypted);
        Assert.Equal(plain, decrypted);
    }

    [Fact]
    public void FakeEncryptionService_ShouldThrowOnEncrypt_ThrowsConfiguredException()
    {
        var fake = new FakeEncryptionService
        {
            ShouldThrowOnEncrypt = true,
            EncryptException = new InvalidOperationException("Custom simulated encrypt error.")
        };

        var ex = Assert.Throws<InvalidOperationException>(() => fake.Encrypt("Test"));
        Assert.Equal("Custom simulated encrypt error.", ex.Message);
    }

    [Fact]
    public void FakeEncryptionService_ShouldThrowOnDecrypt_ThrowsConfiguredException()
    {
        var fake = new FakeEncryptionService
        {
            ShouldThrowOnDecrypt = true,
            DecryptException = new InvalidOperationException("Custom simulated decrypt error.")
        };

        var ex = Assert.Throws<InvalidOperationException>(() => fake.Decrypt("Test"));
        Assert.Equal("Custom simulated decrypt error.", ex.Message);
    }

    [Fact]
    public void EncryptionAssertions_ShouldHaveEncrypted_FailsWhenNotEncrypted()
    {
        var fake = new FakeEncryptionService();
        fake.Encrypt("ItemA");

        Assert.Throws<TrueException>(() => fake.ShouldHaveEncrypted("ItemB"));
    }

    [Fact]
    public void EncryptionAssertions_ShouldHaveDecrypted_FailsWhenNotDecrypted()
    {
        var fake = new FakeEncryptionService();
        fake.Decrypt("[ENC]ItemA");

        Assert.Throws<TrueException>(() => fake.ShouldHaveDecrypted("[ENC]ItemB"));
    }

    #endregion
}
