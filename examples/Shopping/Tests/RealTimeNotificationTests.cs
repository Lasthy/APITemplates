using System.Security.Claims;
using Kernel.Common;
using Kernel.Configuration;
using Kernel.Events;
using Kernel.Json;
using Kernel.Notifications;
using Kernel.Notifications.Extensions;
using Kernel.Testing.Assertions;
using Kernel.Testing.Fakes;
using Kernel.Testing.Infrastructure;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Shopping.Tests;

public class RealTimeNotificationTests : KernelTestBase
{
    // ── 1. NotificationSettings Tests ─────────────────────────────────────────

    [Fact]
    public void NotificationSettings_DefaultValues_ShouldBeEnabledAndDefaultPath()
    {
        var settings = new NotificationSettings(JsonSerializer);

        Assert.Equal("Notifications", settings.SectionName);
        Assert.True(settings.Enabled);
        Assert.Equal("/hubs/notifications", settings.Path);
    }

    [Fact]
    public void NotificationSettings_CustomValues_ShouldReflectChanges()
    {
        var settings = new NotificationSettings(JsonSerializer) { AutoSave = false };

        settings.SetValue("Enabled", false);
        settings.SetValue("Path", "/custom/hub");

        Assert.False(settings.Enabled);
        Assert.Equal("/custom/hub", settings.Path);
    }

    [Fact]
    public void NotificationSettings_InDynamicSettings_ShouldBeAccessible()
    {
        var settings = DynamicSettings.GetSection<NotificationSettings>();

        Assert.NotNull(settings);
        Assert.Equal("Notifications", settings.SectionName);
        Assert.True(settings.Enabled);
    }

    // ── 2. KernelUserIdProvider Tests ─────────────────────────────────────────

    private static HubConnectionContext CreateHubConnectionContext(ClaimsPrincipal? user)
    {
        var connectionContext = new DefaultConnectionContext { User = user };
        return new HubConnectionContext(connectionContext, new HubConnectionContextOptions(), NullLoggerFactory.Instance);
    }

    [Fact]
    public void KernelUserIdProvider_WithSubClaim_ShouldReturnSub()
    {
        var provider = new KernelUserIdProvider();
        var claims = new[] { new Claim("sub", "user_12345") };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));

        var hubConn = CreateHubConnectionContext(principal);

        var userId = provider.GetUserId(hubConn);

        Assert.Equal("user_12345", userId);
    }

    [Fact]
    public void KernelUserIdProvider_WithNameIdentifierClaim_ShouldReturnNameIdentifier()
    {
        var provider = new KernelUserIdProvider();
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, "01ARZ3NDEKTSV4RRFFQ69G5FAV") };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));

        var hubConn = CreateHubConnectionContext(principal);

        var userId = provider.GetUserId(hubConn);

        Assert.Equal("01ARZ3NDEKTSV4RRFFQ69G5FAV", userId);
    }

    [Fact]
    public void KernelUserIdProvider_WithNameClaim_ShouldReturnName()
    {
        var provider = new KernelUserIdProvider();
        var claims = new[] { new Claim(ClaimTypes.Name, "johndoe") };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));

        var hubConn = CreateHubConnectionContext(principal);

        var userId = provider.GetUserId(hubConn);

        Assert.Equal("johndoe", userId);
    }

    [Fact]
    public void KernelUserIdProvider_WithNoUser_ShouldReturnNull()
    {
        var provider = new KernelUserIdProvider();
        var hubConn = CreateHubConnectionContext(null);

        var userId = provider.GetUserId(hubConn);

        Assert.Null(userId);
    }

    // ── 3. FakeNotificationService Tests ──────────────────────────────────────

    [Fact]
    public async Task FakeNotificationService_SendToUser_ShouldRecordProperly()
    {
        var fake = new FakeNotificationService();
        var userId = DefaultId.NewUlid();
        var payload = new { Message = "Hello", Code = 42 };

        await fake.SendToUserAsync(userId, "alerts", payload);

        Assert.Equal(1, fake.Count);
        var notification = fake.SentNotifications[0];
        Assert.Equal(NotificationTarget.User, notification.Target);
        Assert.Equal(userId.ToString(), notification.TargetId);
        Assert.Equal(userId, notification.UserId);
        Assert.Null(notification.Group);
        Assert.False(notification.IsBroadcast);
        Assert.Equal("alerts", notification.Channel);
        Assert.Equal(payload, notification.Payload);

        var userList = fake.GetSentToUser(userId);
        Assert.Single(userList);

        Assert.True(fake.HasSentToUser(userId, "alerts"));
        Assert.False(fake.HasSentToUser(DefaultId.NewUlid(), "alerts"));
    }

    [Fact]
    public async Task FakeNotificationService_SendToGroup_ShouldRecordProperly()
    {
        var fake = new FakeNotificationService();
        var payload = new { Status = "Active" };

        await fake.SendToGroupAsync("Admins", "group-alerts", payload);

        Assert.Equal(1, fake.Count);
        var notification = fake.SentNotifications[0];
        Assert.Equal(NotificationTarget.Group, notification.Target);
        Assert.Equal("Admins", notification.TargetId);
        Assert.Equal("Admins", notification.Group);
        Assert.Null(notification.UserId);
        Assert.False(notification.IsBroadcast);
        Assert.Equal("group-alerts", notification.Channel);

        var groupList = fake.GetSentToGroup("Admins");
        Assert.Single(groupList);

        Assert.True(fake.HasSentToGroup("Admins", "group-alerts"));
        Assert.False(fake.HasSentToGroup("Users", "group-alerts"));
    }

    [Fact]
    public async Task FakeNotificationService_SendToAll_ShouldRecordProperly()
    {
        var fake = new FakeNotificationService();
        var payload = new { Notice = "System Maintenance" };

        await fake.SendToAllAsync("system-broadcast", payload);

        Assert.Equal(1, fake.Count);
        var notification = fake.SentNotifications[0];
        Assert.Equal(NotificationTarget.All, notification.Target);
        Assert.Null(notification.TargetId);
        Assert.Null(notification.UserId);
        Assert.Null(notification.Group);
        Assert.True(notification.IsBroadcast);
        Assert.Equal("system-broadcast", notification.Channel);

        var broadcastList = fake.GetBroadcastNotifications();
        Assert.Single(broadcastList);

        Assert.True(fake.HasSentBroadcast("system-broadcast"));
        Assert.False(fake.HasSentBroadcast("other"));
    }

    [Fact]
    public async Task FakeNotificationService_Clear_ShouldResetState()
    {
        var fake = new FakeNotificationService();
        await fake.SendToAllAsync("test", "data");

        Assert.Equal(1, fake.Count);

        fake.Clear();

        Assert.Equal(0, fake.Count);
        Assert.Empty(fake.SentNotifications);
    }

    // ── 4. NotificationAssertions Tests ───────────────────────────────────────

    [Fact]
    public async Task NotificationAssertions_UserAssertions_ShouldWork()
    {
        var fake = new FakeNotificationService();
        var userId = DefaultId.NewUlid();

        fake.ShouldNotHaveSentNotifications();

        await fake.SendToUserAsync(userId, "order.placed", new { OrderId = 100 });

        fake.ShouldHaveNotificationCount(1);
        fake.ShouldHaveSentToUser(userId);
        fake.ShouldHaveSentToUser(userId, "order.placed");
        fake.ShouldHaveSentToUser(userId, "order.placed", p => p.ToString()!.Contains("100"));

        Assert.ThrowsAny<Exception>(() => fake.ShouldHaveSentToUser(DefaultId.NewUlid()));
        Assert.ThrowsAny<Exception>(() => fake.ShouldHaveSentToUser(userId, "other.channel"));
        Assert.ThrowsAny<Exception>(() => fake.ShouldNotHaveSentNotifications());
    }

    [Fact]
    public async Task NotificationAssertions_GroupAssertions_ShouldWork()
    {
        var fake = new FakeNotificationService();

        await fake.SendToGroupAsync("Managers", "report.ready", new { ReportId = "R1" });

        fake.ShouldHaveSentToGroup("Managers");
        fake.ShouldHaveSentToGroup("Managers", "report.ready");

        Assert.ThrowsAny<Exception>(() => fake.ShouldHaveSentToGroup("Developers"));
        Assert.ThrowsAny<Exception>(() => fake.ShouldHaveSentToGroup("Managers", "other"));
    }

    [Fact]
    public async Task NotificationAssertions_BroadcastAssertions_ShouldWork()
    {
        var fake = new FakeNotificationService();

        await fake.SendToAllAsync("ping", "pong");

        fake.ShouldHaveSentBroadcast();
        fake.ShouldHaveSentBroadcast("ping");

        Assert.ThrowsAny<Exception>(() => fake.ShouldHaveSentBroadcast("pong"));
    }

    // ── 5. SignalRNotificationService Tests ───────────────────────────────────

    [Fact]
    public async Task SignalRNotificationService_WhenDisabled_ShouldNotSend()
    {
        var testHubContext = new TestHubContext();
        var settings = new NotificationSettings(JsonSerializer) { AutoSave = false };
        settings.SetValue("Enabled", false);

        var service = new SignalRNotificationService(
            testHubContext,
            settings,
            NullLogger<SignalRNotificationService>.Instance);

        var userId = DefaultId.NewUlid();
        await service.SendToUserAsync(userId, "test", "data");
        await service.SendToGroupAsync("group", "test", "data");
        await service.SendToAllAsync("test", "data");

        Assert.Empty(testHubContext.ClientsHolder.UserProxies);
        Assert.Empty(testHubContext.ClientsHolder.GroupProxies);
        Assert.Empty(testHubContext.ClientsHolder.AllProxy.Invocations);
    }

    [Fact]
    public async Task SignalRNotificationService_WhenEnabled_ShouldSendToHubClients()
    {
        var testHubContext = new TestHubContext();
        var settings = new NotificationSettings(JsonSerializer) { AutoSave = false };
        settings.SetValue("Enabled", true);

        var service = new SignalRNotificationService(
            testHubContext,
            settings,
            NullLogger<SignalRNotificationService>.Instance);

        var userId = DefaultId.NewUlid();
        await service.SendToUserAsync(userId, "order.status", new { Status = "Shipped" });
        await service.SendToGroupAsync("Admins", "admin.alert", new { Level = "High" });
        await service.SendToAllAsync("broadcast.event", new { Message = "Global" });

        Assert.True(testHubContext.ClientsHolder.UserProxies.ContainsKey(userId.ToString()));
        Assert.True(testHubContext.ClientsHolder.GroupProxies.ContainsKey("Admins"));
        Assert.NotEmpty(testHubContext.ClientsHolder.AllProxy.Invocations);

        // Check user invocations
        var userProxy = testHubContext.ClientsHolder.UserProxies[userId.ToString()];
        Assert.Contains(userProxy.Invocations, inv => inv.Method == "ReceiveNotification" && inv.Args[0]?.ToString() == "order.status");
        Assert.Contains(userProxy.Invocations, inv => inv.Method == "order.status");

        // Check group invocations
        var groupProxy = testHubContext.ClientsHolder.GroupProxies["Admins"];
        Assert.Contains(groupProxy.Invocations, inv => inv.Method == "ReceiveNotification" && inv.Args[0]?.ToString() == "admin.alert");
        Assert.Contains(groupProxy.Invocations, inv => inv.Method == "admin.alert");

        // Check broadcast invocations
        Assert.Contains(testHubContext.ClientsHolder.AllProxy.Invocations, inv => inv.Method == "ReceiveNotification" && inv.Args[0]?.ToString() == "broadcast.event");
        Assert.Contains(testHubContext.ClientsHolder.AllProxy.Invocations, inv => inv.Method == "broadcast.event");
    }

    // ── 6. NotificationHub Tests ──────────────────────────────────────────────

    [Fact]
    public async Task NotificationHub_JoinAndLeaveGroup_ShouldInteractWithGroups()
    {
        var hub = new NotificationHub(NullLogger<NotificationHub>.Instance);
        var groupManager = new TestGroupManager();
        var context = new TestHubCallerContext("conn_123", null);

        hub.Context = context;
        hub.Groups = groupManager;

        await hub.JoinGroup("TestGroup");
        Assert.Contains(groupManager.AddedToGroup, item => item.ConnectionId == "conn_123" && item.GroupName == "TestGroup");

        await hub.LeaveGroup("TestGroup");
        Assert.Contains(groupManager.RemovedFromGroup, item => item.ConnectionId == "conn_123" && item.GroupName == "TestGroup");

        // Empty group should be ignored
        await hub.JoinGroup("");
        await hub.LeaveGroup(" ");
        Assert.DoesNotContain(groupManager.AddedToGroup, item => string.IsNullOrWhiteSpace(item.GroupName));
        Assert.DoesNotContain(groupManager.RemovedFromGroup, item => string.IsNullOrWhiteSpace(item.GroupName));
    }

    // ── 7. EventBus & Event Handler Integration ───────────────────────────────

    public record TestOrderShippedEvent(DefaultId OrderId, DefaultId UserId, string TrackingNumber) : Event;

    public class TestOrderShippedNotificationHandler : IEventHandler<TestOrderShippedEvent>
    {
        private readonly INotificationService _notifications;

        public TestOrderShippedNotificationHandler(INotificationService notifications)
        {
            _notifications = notifications;
        }

        public async Task HandleAsync(TestOrderShippedEvent @event, CancellationToken ct = default)
        {
            await _notifications.SendToUserAsync(
                @event.UserId,
                "OrderShipped",
                new { @event.OrderId, @event.TrackingNumber },
                ct);

            await _notifications.SendToGroupAsync(
                "WarehouseAdmins",
                "OrderDispatched",
                new { @event.OrderId },
                ct);
        }
    }

    [Fact]
    public async Task EventBus_WithNotificationHandler_ShouldPushRealTimeNotifications()
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        services.AddScoped<IEventHandler<TestOrderShippedEvent>, TestOrderShippedNotificationHandler>();

        var provider = services.BuildServiceProvider();
        var inProcessBus = new InProcessEventBus(provider);
        var notificationService = provider.GetRequiredService<FakeNotificationService>();

        var userId = DefaultId.NewUlid();
        var orderId = DefaultId.NewUlid();
        var domainEvent = new TestOrderShippedEvent(orderId, userId, "TRACK-98765");

        await inProcessBus.PublishAsync(domainEvent);

        notificationService.ShouldHaveNotificationCount(2);
        notificationService.ShouldHaveSentToUser(userId, "OrderShipped");
        notificationService.ShouldHaveSentToGroup("WarehouseAdmins", "OrderDispatched");
    }

    // ── 8. DI & Pipeline Integration ──────────────────────────────────────────

    [Fact]
    public void KernelTestBase_ShouldProvideNotificationServiceAndSettings()
    {
        Assert.NotNull(NotificationService);
        var resolvedService = Services.GetService<INotificationService>();
        Assert.NotNull(resolvedService);
        Assert.Same(NotificationService, resolvedService);

        var settings = Services.GetService<NotificationSettings>();
        Assert.NotNull(settings);
        Assert.Equal("Notifications", settings.SectionName);
    }

    [Fact]
    public async Task NotificationHub_EndpointMapping_ShouldRespondToNegotiate()
    {
        using var factory = new TestWebApplicationFactory<Program>();
        var client = factory.CreateClient();

        var response = await client.PostAsync("/hubs/notifications/negotiate?negotiateVersion=1", null);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    // ── SignalR Test Doubles ──────────────────────────────────────────────────

    private class TestHubContext : IHubContext<NotificationHub>
    {
        public TestHubClients ClientsHolder { get; } = new();
        public IHubClients Clients => ClientsHolder;
        public IGroupManager Groups => throw new NotImplementedException();
    }

    private class TestHubClients : IHubClients
    {
        public Dictionary<string, TestClientProxy> UserProxies { get; } = new();
        public Dictionary<string, TestClientProxy> GroupProxies { get; } = new();
        public TestClientProxy AllProxy { get; } = new();

        public IClientProxy All => AllProxy;
        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => throw new NotImplementedException();
        public IClientProxy Client(string connectionId) => throw new NotImplementedException();
        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => throw new NotImplementedException();
        public IClientProxy Group(string groupName) => GroupProxies.TryGetValue(groupName, out var p) ? p : (GroupProxies[groupName] = new TestClientProxy());
        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => throw new NotImplementedException();
        public IClientProxy Groups(IReadOnlyList<string> groupNames) => throw new NotImplementedException();
        public IClientProxy User(string userId) => UserProxies.TryGetValue(userId, out var p) ? p : (UserProxies[userId] = new TestClientProxy());
        public IClientProxy Users(IReadOnlyList<string> userIds) => throw new NotImplementedException();
    }

    private class TestClientProxy : IClientProxy
    {
        public List<(string Method, object?[] Args)> Invocations { get; } = [];

        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
        {
            Invocations.Add((method, args));
            return Task.CompletedTask;
        }
    }

    private class TestHubCallerContext : HubCallerContext
    {
        public override string ConnectionId { get; }
        public override ClaimsPrincipal? User { get; }
        public override string? UserIdentifier => User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();
        public override IFeatureCollection Features { get; } = new FeatureCollection();
        public override CancellationToken ConnectionAborted => CancellationToken.None;

        public TestHubCallerContext(string connectionId, ClaimsPrincipal? user)
        {
            ConnectionId = connectionId;
            User = user;
        }

        public override void Abort()
        {
        }
    }

    private class TestGroupManager : IGroupManager
    {
        public List<(string ConnectionId, string GroupName)> AddedToGroup { get; } = [];
        public List<(string ConnectionId, string GroupName)> RemovedFromGroup { get; } = [];

        public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        {
            AddedToGroup.Add((connectionId, groupName));
            return Task.CompletedTask;
        }

        public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        {
            RemovedFromGroup.Add((connectionId, groupName));
            return Task.CompletedTask;
        }
    }
}
