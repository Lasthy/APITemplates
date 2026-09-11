using System.Security.Claims;
using Kernel.Json;
using Kernel.Notifications;
using Kernel.Testing.Assertions;
using Kernel.Testing.Fakes;
using Microsoft.AspNetCore.SignalR;
using Xunit;

namespace Kernel.Tests.Notifications;

public class NotificationsTests
{
    [Fact]
    public void NotificationSettings_Defaults_ShouldBeValid()
    {
        var serializer = new Kernel.Json.JsonSerializer();
        var settings = new NotificationSettings(serializer) { AutoSave = false };

        Assert.True(settings.Enabled);
        Assert.Equal("/hubs/notifications", settings.Path);
    }

    [Fact]
    public void KernelUserIdProvider_ShouldResolveFromClaims()
    {
        var provider = new KernelUserIdProvider();

        // sub claim
        var subClaims = new[] { new Claim("sub", "user-sub-123") };
        var subPrincipal = new ClaimsPrincipal(new ClaimsIdentity(subClaims));
        var subContext = CreateHubConnectionContext(subPrincipal);
        Assert.Equal("user-sub-123", provider.GetUserId(subContext));

        // NameIdentifier claim
        var nameIdClaims = new[] { new Claim(ClaimTypes.NameIdentifier, "user-nameid-456") };
        var nameIdPrincipal = new ClaimsPrincipal(new ClaimsIdentity(nameIdClaims));
        var nameIdContext = CreateHubConnectionContext(nameIdPrincipal);
        Assert.Equal("user-nameid-456", provider.GetUserId(nameIdContext));
    }

    [Fact]
    public async Task FakeNotificationService_And_Assertions_ShouldTrackNotifications()
    {
        var fake = new FakeNotificationService();
        var userId = DefaultId.NewUlid();

        await fake.SendToUserAsync(userId, "order.shipped", new { TrackingNumber = "TRK123" });
        await fake.SendToGroupAsync("admins", "alert", new { Message = "High CPU" });
        await fake.SendToAllAsync("system.announcement", new { Text = "Maintenance" });

        fake.ShouldHaveSentToUser(userId);
        fake.ShouldHaveSentToGroup("admins");
        fake.ShouldHaveSentBroadcast();
        fake.ShouldHaveNotificationCount(3);

        Assert.True(fake.HasSentToUser(userId, "order.shipped"));
        Assert.True(fake.HasSentToGroup("admins", "alert"));
        Assert.True(fake.HasSentBroadcast("system.announcement"));
    }

    private static HubConnectionContext CreateHubConnectionContext(ClaimsPrincipal user)
    {
        var context = (HubConnectionContext)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(HubConnectionContext));
        var userField = typeof(HubConnectionContext).GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .FirstOrDefault(f => f.FieldType == typeof(ClaimsPrincipal));
        userField?.SetValue(context, user);

        var userProp = typeof(HubConnectionContext).GetProperty("User");
        if (userProp?.CanWrite == true)
        {
            userProp.SetValue(context, user);
        }
        return context;
    }
}
