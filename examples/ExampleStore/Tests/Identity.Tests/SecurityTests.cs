using System.Text.Json;
using Identity.Configuration;
using Identity.Persistence;
using Kernel.Configuration;
using Kernel.Configuration.Features;
using Kernel.Email;
using Kernel.Encryption;
using Kernel.RateLimiting;
using Kernel.Testing.Assertions;
using Kernel.Testing.Infrastructure;
using Xunit;

namespace Identity.Tests;

public class SecurityTests : ModuleTestBase<IdentityModule, IdentityModuleDbContext>
{
    [Fact]
    public async Task GetSettings_RedactsSensitiveProperties()
    {
        // 1. Check Encryption settings
        var encResult = await Dispatcher.DispatchAsync(new GetSettings("Encryption"));
        var encJson = encResult.ShouldBeSuccess();
        using var encDoc = JsonDocument.Parse(encJson);
        Assert.Equal("******", encDoc.RootElement.GetProperty("Key").GetString());

        // 2. Check Identity settings
        var identitySettings = new IdentitySettings(JsonSerializer, "Testing") { AutoSave = false };
        DynamicSettings.AddSection(identitySettings);

        var idResult = await Dispatcher.DispatchAsync(new GetSettings("Identity"));
        var idJson = idResult.ShouldBeSuccess();
        using var idDoc = JsonDocument.Parse(idJson);
        Assert.Equal("******", idDoc.RootElement.GetProperty("JwtSecret").GetString());
        Assert.Equal("******", idDoc.RootElement.GetProperty("SeedAdminPassword").GetString());

        // Non-sensitive properties should remain intact
        Assert.Equal("mwa", idDoc.RootElement.GetProperty("JwtIssuer").GetString());

        // 3. Check Email settings
        var emailSection = DynamicSettings.GetSection<EmailSettings>();
        emailSection.SetValue("SmtpPassword", "SecretPass123");
        var emailResult = await Dispatcher.DispatchAsync(new GetSettings("Email"));
        var emailJson = emailResult.ShouldBeSuccess();
        using var emailDoc = JsonDocument.Parse(emailJson);
        Assert.Equal("******", emailDoc.RootElement.GetProperty("SmtpPassword").GetString());
        Assert.Equal("smtp.example.com", emailDoc.RootElement.GetProperty("SmtpHost").GetString());

        // 4. Check Data settings
        var dataResult = await Dispatcher.DispatchAsync(new GetSettings("Data"));
        var dataJson = dataResult.ShouldBeSuccess();
        using var dataDoc = JsonDocument.Parse(dataJson);
        Assert.Equal("******", dataDoc.RootElement.GetProperty("ConnectionString").GetString());
        Assert.Equal("InMemory", dataDoc.RootElement.GetProperty("Provider").GetString());
    }

    [Fact]
    public async Task UpdateSettings_BlocksMaskedPlaceholderValues()
    {
        using var doc = JsonDocument.Parse("\"******\"");
        var result = await Dispatcher.DispatchAsync(new UpdateSettings("Storage", "BaseUrl", doc.RootElement.Clone()));

        result.ShouldBeFailure();
        Assert.Contains("Cannot set masked placeholder value", result.Error?.Message);
    }

    [Fact]
    public async Task UpdateSettings_BlocksSensitiveKeyMutation()
    {
        using var keyDoc = JsonDocument.Parse("\"new-secret-key-12345678901234567890\"");
        var result = await Dispatcher.DispatchAsync(new UpdateSettings("Encryption", "Key", keyDoc.RootElement.Clone()));

        result.ShouldBeFailure();
        Assert.Contains("prohibited via the management API", result.Error?.Message);
    }

    [Fact]
    public async Task UpdateSettings_AllowsNonSensitiveKeyMutation()
    {
        using var valDoc = JsonDocument.Parse("123456");
        var result = await Dispatcher.DispatchAsync(new UpdateSettings("Storage", "MaxFileSizeBytes", valDoc.RootElement.Clone()));

        result.ShouldBeSuccess();
    }

    [Fact]
    public void EncryptionSettings_ThrowsInProduction_WhenKeyIsDefault()
    {
        var settings = new EncryptionSettings(JsonSerializer, "Production") { AutoSave = false };

        var ex = Assert.Throws<InvalidOperationException>(() => settings.GetKeyBytes());
        Assert.Contains("In production environments, a secure, non-default 256-bit AES encryption key must be configured", ex.Message);
    }

    [Fact]
    public void EncryptionSettings_SucceedsInDevelopment_WithDefaultKey()
    {
        var settings = new EncryptionSettings(JsonSerializer, "Development") { AutoSave = false };

        var keyBytes = settings.GetKeyBytes();
        Assert.Equal(32, keyBytes.Length);
    }

    [Fact]
    public void IdentitySettings_DetectsDefaultJwtSecret()
    {
        var settings = new IdentitySettings(JsonSerializer, "Production") { AutoSave = false };
        Assert.True(settings.IsDefaultJwtSecret);

        settings.SetValue("JwtSecret", "CustomSecureSecretKeyForTestingThatIsLongEnough123456!");
        Assert.False(settings.IsDefaultJwtSecret);
    }

    [Fact]
    public void IdentitySettings_DetectsDefaultAdminPassword()
    {
        var settings = new IdentitySettings(JsonSerializer, "Production") { AutoSave = false };
        Assert.True(settings.IsDefaultSeedAdminPassword);

        settings.SetValue("SeedAdminPassword", "DifferentPassword123!");
        Assert.False(settings.IsDefaultSeedAdminPassword);
    }

    [Fact]
    public void RateLimitingSettings_DefaultsToNotTrustingForwardedHeaders()
    {
        var settings = new RateLimitingSettings(JsonSerializer, "Production") { AutoSave = false };
        Assert.False(settings.TrustForwardedHeaders);
    }
}
