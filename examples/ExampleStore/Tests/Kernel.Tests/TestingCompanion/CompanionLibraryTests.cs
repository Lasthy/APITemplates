using Kernel.Configuration;
using Kernel.Constants;
using Kernel.Email;
using Kernel.Events;
using Kernel.Testing.Assertions;
using Kernel.Testing.Fakes;
using Xunit;

namespace Kernel.Tests.TestingCompanion;

public class CompanionLibraryTests
{
    private record TestEvent(string Message) : Event;

    [Fact]
    public async Task FakeEventBus_And_Assertions_ShouldTrackEvents()
    {
        var bus = new FakeEventBus();
        bus.ShouldBeEmpty();

        await bus.PublishAsync(new TestEvent("Hello"));

        bus.ShouldHavePublished<TestEvent>();
        bus.ShouldHavePublished<TestEvent>(e => e.Message == "Hello");
        bus.ShouldHavePublished<TestEvent>(expectedCount: 1);
        bus.ShouldNotHavePublished<SettingsChangedEvent>();

        bus.Clear();
        bus.ShouldBeEmpty();
    }

    [Fact]
    public void FakeScopeContext_FluentSetup_ShouldConfigureIdentity()
    {
        var scope = new FakeScopeContext();
        Assert.False(scope.IsAuthenticated);

        var userId = DefaultId.NewUlid();
        scope.AsAuthenticated(userId, ["Manager", "Auditor"]);

        Assert.True(scope.IsAuthenticated);
        Assert.Equal(userId, scope.UserId);
        Assert.True(scope.IsInRole("Manager"));
        Assert.False(scope.IsSystem);

        scope.AsSystem();
        Assert.True(scope.IsSystem);
        Assert.True(scope.IsInRole(Roles.System));

        scope.WithCorrelationId("custom-corr-id");
        Assert.Equal("custom-corr-id", scope.CorrelationId);

        scope.Reset();
        Assert.False(scope.IsAuthenticated);
        Assert.Null(scope.UserId);
    }

    [Fact]
    public async Task FakeEmailSender_And_Assertions_ShouldTrackEmails()
    {
        var emailSender = new FakeEmailSender();
        emailSender.ShouldNotHaveSent();

        var message = new EmailMessage
        {
            To = "recipient@example.com",
            Subject = "Welcome",
            Body = "Welcome to our platform!"
        };

        await emailSender.SendAsync(message);

        emailSender.ShouldHaveSent();
        emailSender.ShouldHaveSentTo("recipient@example.com");
        emailSender.ShouldHaveSentCount(1);
        Assert.Equal("Welcome", emailSender.LastSentMessage?.Subject);
    }

    [Fact]
    public async Task FakeCacheService_And_Assertions_ShouldTrackCachedItems()
    {
        var cache = new FakeCacheService();
        cache.ShouldBeEmpty();

        await cache.SetAsync("profile:1", "ProfileData", TimeSpan.FromMinutes(10));

        cache.ShouldContainKey("profile:1");
        cache.ShouldContain("profile:1", "ProfileData");
        cache.ShouldNotContainKey("profile:999");
        cache.ShouldHaveCount(1);

        await cache.RemoveAsync("profile:1");
        cache.ShouldNotContainKey("profile:1");
    }

    [Fact]
    public void FakeEncryptionService_And_Assertions_ShouldTrackOperations()
    {
        var encryption = new FakeEncryptionService();
        encryption.ShouldBeEmpty();

        var ciphertext = encryption.Encrypt("PlainSecret");
        var decrypted = encryption.Decrypt(ciphertext);

        Assert.Equal("PlainSecret", decrypted);
        encryption.ShouldHaveEncrypted("PlainSecret");
        encryption.ShouldHaveDecrypted(ciphertext);
        encryption.ShouldHaveEncryptedCount(1);
        encryption.ShouldHaveDecryptedCount(1);
    }

    [Fact]
    public async Task FakeFileStorage_And_Assertions_ShouldTrackFiles()
    {
        var storage = new FakeFileStorage();
        storage.ShouldBeEmpty();

        using var stream = new MemoryStream("File content"u8.ToArray());
        await storage.UploadAsync("uploads/doc.pdf", stream);

        storage.ShouldHaveFile("uploads/doc.pdf");
        storage.ShouldNotHaveFile("uploads/missing.pdf");
        storage.ShouldHaveCount(1);

        await storage.DeleteAsync("uploads/doc.pdf");
        storage.ShouldHaveDeleted("uploads/doc.pdf");
        storage.ShouldNotHaveFile("uploads/doc.pdf");
    }
}
