using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Kernel.AuditLog;
using Kernel.AuditLog.Features;
using Kernel.Boxing;
using Kernel.Common;
using Kernel.Constants;
using Kernel.Context;
using Kernel.Json;
using Kernel.Persistence;
using Kernel.Requests;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Shopping.Tests;

public class AuditLogTests : IClassFixture<ShoppingApiTestFactory>
{
    private readonly ShoppingApiTestFactory _factory;

    public AuditLogTests(ShoppingApiTestFactory factory)
    {
        _factory = factory;
    }

    #region Unit & Model Tests

    [Fact]
    public void AuditEntry_Initialization_ShouldSetDefaultValues()
    {
        // Arrange & Act
        var entry = new AuditEntry
        {
            EntityType = "Product",
            EntityId = "prod-1",
            Action = AuditAction.Create
        };

        // Assert
        Assert.NotEqual(default, entry.Id);
        Assert.Equal("Product", entry.EntityType);
        Assert.Equal("prod-1", entry.EntityId);
        Assert.Equal(AuditAction.Create, entry.Action);
        Assert.Null(entry.UserId);
        Assert.Null(entry.Changes);
        Assert.Null(entry.CorrelationId);
        Assert.True(entry.OccurredAt <= DateTime.UtcNow);
    }

    [Fact]
    public void AuditPropertyChange_Properties_ShouldBeAccessible()
    {
        // Arrange & Act
        var change = new AuditPropertyChange("Price", "10.00", "15.00");

        // Assert
        Assert.Equal("Price", change.PropertyName);
        Assert.Equal("10.00", change.OldValue);
        Assert.Equal("15.00", change.NewValue);
    }

    #endregion

    #region Interceptor Tests

    [Fact]
    public async Task AuditLogSaveChangesInterceptor_OnAddedEntity_ShouldCaptureCreateAuditEntry()
    {
        // Arrange
        var userId = DefaultId.NewUlid();
        var correlationId = "corr-" + DefaultId.NewUlid();
        var fakeContext = new FakeScopeContext(userId, correlationId);
        var jsonSerializer = new Kernel.Json.JsonSerializer();

        var services = new ServiceCollection();
        var dbName = "AuditTest_" + Guid.NewGuid().ToString("N");
        services.AddDbContext<KernelDbContext>(opt => opt.UseInMemoryDatabase(dbName));
        var sp = services.BuildServiceProvider();

        var interceptor = new AuditLogSaveChangesInterceptor(sp, fakeContext, jsonSerializer);

        var options = new DbContextOptionsBuilder<TestModuleDbContext>()
            .UseInMemoryDatabase(dbName + "_module")
            .AddInterceptors(interceptor)
            .Options;

        using var moduleDb = new TestModuleDbContext(options);
        var entity = new TestEntity { Name = "Initial Entity", Price = 100m };

        // Act
        moduleDb.TestEntities.Add(entity);
        await moduleDb.SaveChangesAsync();

        // Assert
        using var kernelDb = sp.GetRequiredService<KernelDbContext>();
        var audit = await kernelDb.AuditEntries.FirstOrDefaultAsync(a => a.EntityType == nameof(TestEntity));

        Assert.NotNull(audit);
        Assert.Equal(AuditAction.Create, audit.Action);
        Assert.Equal(entity.Id.ToString(), audit.EntityId);
        Assert.Equal(userId, audit.UserId);
        Assert.Equal(correlationId, audit.CorrelationId);
        Assert.NotNull(audit.Changes);

        var changes = jsonSerializer.Deserialize<List<AuditPropertyChange>>(audit.Changes);
        Assert.NotNull(changes);
        Assert.Contains(changes, c => c.PropertyName == nameof(TestEntity.Name) && c.NewValue == "Initial Entity" && c.OldValue == null);
        Assert.Contains(changes, c => c.PropertyName == nameof(TestEntity.Price) && c.NewValue == "100" && c.OldValue == null);
    }

    [Fact]
    public async Task AuditLogSaveChangesInterceptor_OnModifiedEntity_ShouldCaptureOnlyChangedProperties()
    {
        // Arrange
        var userId = DefaultId.NewUlid();
        var fakeContext = new FakeScopeContext(userId, "corr-update");
        var jsonSerializer = new Kernel.Json.JsonSerializer();

        var services = new ServiceCollection();
        var dbName = "AuditTest_" + Guid.NewGuid().ToString("N");
        services.AddDbContext<KernelDbContext>(opt => opt.UseInMemoryDatabase(dbName));
        var sp = services.BuildServiceProvider();

        var interceptor = new AuditLogSaveChangesInterceptor(sp, fakeContext, jsonSerializer);

        var options = new DbContextOptionsBuilder<TestModuleDbContext>()
            .UseInMemoryDatabase(dbName + "_module")
            .AddInterceptors(interceptor)
            .Options;

        using var moduleDb = new TestModuleDbContext(options);
        var entity = new TestEntity { Name = "Original Name", Price = 50m };
        moduleDb.TestEntities.Add(entity);
        await moduleDb.SaveChangesAsync();

        // Act - Update only the price
        entity.Price = 75m;
        await moduleDb.SaveChangesAsync();

        // Assert
        using var kernelDb = sp.GetRequiredService<KernelDbContext>();
        var updates = await kernelDb.AuditEntries
            .Where(a => a.EntityType == nameof(TestEntity) && a.Action == AuditAction.Update)
            .ToListAsync();

        Assert.Single(updates);
        var audit = updates[0];
        Assert.Equal(entity.Id.ToString(), audit.EntityId);
        Assert.NotNull(audit.Changes);

        var changes = jsonSerializer.Deserialize<List<AuditPropertyChange>>(audit.Changes);
        Assert.NotNull(changes);
        Assert.Single(changes);
        Assert.Equal(nameof(TestEntity.Price), changes[0].PropertyName);
        Assert.Equal("50", changes[0].OldValue);
        Assert.Equal("75", changes[0].NewValue);
    }

    [Fact]
    public async Task AuditLogSaveChangesInterceptor_OnDeletedEntity_ShouldCaptureDeleteAuditEntry()
    {
        // Arrange
        var fakeContext = new FakeScopeContext(DefaultId.NewUlid(), "corr-delete");
        var jsonSerializer = new Kernel.Json.JsonSerializer();

        var services = new ServiceCollection();
        var dbName = "AuditTest_" + Guid.NewGuid().ToString("N");
        services.AddDbContext<KernelDbContext>(opt => opt.UseInMemoryDatabase(dbName));
        var sp = services.BuildServiceProvider();

        var interceptor = new AuditLogSaveChangesInterceptor(sp, fakeContext, jsonSerializer);

        var options = new DbContextOptionsBuilder<TestModuleDbContext>()
            .UseInMemoryDatabase(dbName + "_module")
            .AddInterceptors(interceptor)
            .Options;

        using var moduleDb = new TestModuleDbContext(options);
        var entity = new TestEntity { Name = "To Be Deleted", Price = 20m };
        moduleDb.TestEntities.Add(entity);
        await moduleDb.SaveChangesAsync();

        // Act
        moduleDb.TestEntities.Remove(entity);
        await moduleDb.SaveChangesAsync();

        // Assert
        using var kernelDb = sp.GetRequiredService<KernelDbContext>();
        var deletes = await kernelDb.AuditEntries
            .Where(a => a.EntityType == nameof(TestEntity) && a.Action == AuditAction.Delete)
            .ToListAsync();

        Assert.Single(deletes);
        var audit = deletes[0];
        Assert.Equal(entity.Id.ToString(), audit.EntityId);
        Assert.NotNull(audit.Changes);

        var changes = jsonSerializer.Deserialize<List<AuditPropertyChange>>(audit.Changes);
        Assert.NotNull(changes);
        Assert.Contains(changes, c => c.PropertyName == nameof(TestEntity.Name) && c.OldValue == "To Be Deleted" && c.NewValue == null);
    }

    [Fact]
    public async Task AuditLogSaveChangesInterceptor_ShouldNotAuditAuditEntryOrMessage()
    {
        // Arrange
        var jsonSerializer = new Kernel.Json.JsonSerializer();
        var services = new ServiceCollection();
        var dbName = "AuditTest_NoRecurse_" + Guid.NewGuid().ToString("N");
        services.AddDbContext<KernelDbContext>(opt => opt.UseInMemoryDatabase(dbName));
        var sp = services.BuildServiceProvider();

        var interceptor = new AuditLogSaveChangesInterceptor(sp, null, jsonSerializer);

        var kernelOptions = new DbContextOptionsBuilder<KernelDbContext>()
            .UseInMemoryDatabase(dbName)
            .AddInterceptors(interceptor)
            .Options;

        using var kernelDb = new KernelDbContext(kernelOptions);

        // Act - Adding a Message and an AuditEntry directly to KernelDbContext
        kernelDb.Messages.Add(new Message
        {
            Type = "TestEvent",
            Content = "{}",
            Channel = MessageChannel.Event,
            Partition = 0
        });

        kernelDb.AuditEntries.Add(new AuditEntry
        {
            EntityType = "ManualAudit",
            EntityId = "1",
            Action = AuditAction.Create
        });

        await kernelDb.SaveChangesAsync();

        // Assert - Only the 1 manually added AuditEntry should exist, no duplicate recursive audit entries
        var totalAuditCount = await kernelDb.AuditEntries.CountAsync();
        Assert.Equal(1, totalAuditCount);
    }

    #endregion

    #region Service & Handler Query Tests

    [Fact]
    public async Task AuditLogService_FilteringAndSorting_ShouldReturnExpectedResults()
    {
        // Arrange
        var dbName = "AuditServiceTest_" + Guid.NewGuid().ToString("N");
        var options = new DbContextOptionsBuilder<KernelDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        var targetUserId = DefaultId.NewUlid();
        var otherUserId = DefaultId.NewUlid();
        var targetCorrelationId = "target-corr";

        using (var seedDb = new KernelDbContext(options))
        {
            seedDb.AuditEntries.AddRange(
                new AuditEntry
                {
                    EntityType = "Product",
                    EntityId = "p-1",
                    Action = AuditAction.Create,
                    UserId = targetUserId,
                    CorrelationId = targetCorrelationId,
                    OccurredAt = DateTime.UtcNow.AddMinutes(-30)
                },
                new AuditEntry
                {
                    EntityType = "Product",
                    EntityId = "p-1",
                    Action = AuditAction.Update,
                    UserId = targetUserId,
                    CorrelationId = targetCorrelationId,
                    OccurredAt = DateTime.UtcNow.AddMinutes(-20)
                },
                new AuditEntry
                {
                    EntityType = "Order",
                    EntityId = "o-1",
                    Action = AuditAction.Create,
                    UserId = otherUserId,
                    CorrelationId = "other-corr",
                    OccurredAt = DateTime.UtcNow.AddMinutes(-10)
                }
            );
            await seedDb.SaveChangesAsync();
        }

        using var queryDb = new KernelDbContext(options);
        var service = new AuditLogService(queryDb);

        // Act 1: Filter by EntityType
        var productResult = await service.GetEntriesAsync(new GetAuditEntries(EntityType: "Product"));
        Assert.Equal(2, productResult.TotalCount);
        Assert.All(productResult.Items, i => Assert.Equal("Product", i.EntityType));

        // Act 2: Filter by UserId
        var userResult = await service.GetEntriesAsync(new GetAuditEntries(UserId: targetUserId));
        Assert.Equal(2, userResult.TotalCount);

        // Act 3: Filter by Action
        var createResult = await service.GetEntriesAsync(new GetAuditEntries(Action: AuditAction.Create));
        Assert.Equal(2, createResult.TotalCount);

        // Act 4: Filter by CorrelationId
        var corrResult = await service.GetEntriesAsync(new GetAuditEntries(CorrelationId: targetCorrelationId));
        Assert.Equal(2, corrResult.TotalCount);

        // Act 5: Default sort descending by OccurredAt
        var allResult = await service.GetEntriesAsync(new GetAuditEntries());
        Assert.Equal(3, allResult.TotalCount);
        var list = allResult.Items.ToList();
        Assert.Equal("Order", list[0].EntityType); // most recent
        Assert.Equal("p-1", list[1].EntityId);
        Assert.Equal(AuditAction.Update, list[1].Action);
        Assert.Equal(AuditAction.Create, list[2].Action);
    }

    [Fact]
    public async Task GetAuditEntriesHandler_ViaDispatcher_ShouldReturnPaginatedResult()
    {
        // Arrange
        var dbName = "AuditHandlerTest_" + Guid.NewGuid().ToString("N");
        var options = new DbContextOptionsBuilder<KernelDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        using (var seedDb = new KernelDbContext(options))
        {
            seedDb.AuditEntries.Add(new AuditEntry
            {
                EntityType = "Customer",
                EntityId = "c-100",
                Action = AuditAction.Create
            });
            await seedDb.SaveChangesAsync();
        }

        using var db = new KernelDbContext(options);
        var service = new AuditLogService(db);
        var handler = new GetAuditEntriesHandler(service);

        // Act
        var result = await handler.HandleAsync(new GetAuditEntries(EntityType: "Customer"));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(1, result.Value.TotalCount);
        Assert.Equal("c-100", result.Value.Items.First().EntityId);
    }

    #endregion

    #region Controller & Integration Tests

    [Fact]
    public async Task AuditController_WhenAnonymous_ShouldReturn401()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/v1/kernel/audit");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AuditController_WhenRegularUser_ShouldReturn403()
    {
        // Arrange
        var client = _factory.CreateClient();
        var token = await _factory.LoginAsync(client, "client@shopping.local", "Password123!");
        var authClient = _factory.CreateAuthenticatedClient(token);

        // Act
        var response = await authClient.GetAsync("/api/v1/kernel/audit");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AuditController_WhenSystemUser_ShouldReturn200AndAuditEntries()
    {
        // Arrange
        var client = _factory.CreateClient();
        var token = await _factory.LoginAsync(client, "admin@localhost", "Admin123!");
        var adminClient = _factory.CreateAuthenticatedClient(token);

        // Act
        var response = await adminClient.GetAsync("/api/v1/kernel/audit?page=1&pageSize=50");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("items", out var items) || body.TryGetProperty("value", out items));
    }

    [Fact]
    public async Task ShoppingFlow_Mutations_ShouldGenerateStructuredAuditTrail()
    {
        // Arrange
        var client = _factory.CreateClient();
        var token = await _factory.LoginAsync(client, "admin@localhost", "Admin123!");
        var adminClient = _factory.CreateAuthenticatedClient(token);

        // Act 1: Create a product via Catalog API
        var productSku = "AUDIT-PROD-" + Guid.NewGuid().ToString("N")[..8];
        var createResponse = await adminClient.PostAsJsonAsync("/api/v1/catalog/products", new
        {
            Name = "Audit Tested Product",
            Description = "Audit Description",
            Sku = productSku,
            Price = 49.99m,
            StockQuantity = 10,
            LowStockThreshold = 2
        });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        // Act 2: Query the audit log via AuditController for this entity
        var auditResponse = await adminClient.GetAsync($"/api/v1/kernel/audit?entityType=Product&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, auditResponse.StatusCode);

        var auditJson = await auditResponse.Content.ReadFromJsonAsync<JsonElement>();
        var itemsElement = auditJson.GetProperty("items");
        Assert.True(itemsElement.GetArrayLength() > 0);

        var matchedAudit = itemsElement.EnumerateArray()
            .FirstOrDefault(e => e.TryGetProperty("changes", out var ch) &&
                                 ch.GetString()?.Contains(productSku, StringComparison.OrdinalIgnoreCase) == true);

        Assert.NotEqual(JsonValueKind.Undefined, matchedAudit.ValueKind);
        Assert.Equal("Product", matchedAudit.GetProperty("entityType").GetString());

        var actionElement = matchedAudit.GetProperty("action");
        var isCreate = (actionElement.ValueKind == JsonValueKind.Number && actionElement.GetInt32() == (int)AuditAction.Create)
            || (actionElement.ValueKind == JsonValueKind.String && actionElement.GetString() == nameof(AuditAction.Create));
        Assert.True(isCreate);
    }

    #endregion

    #region Helper Types

    private class TestEntity
    {
        public DefaultId Id { get; set; } = DefaultId.NewUlid();
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
    }

    private class TestModuleDbContext : DbContext
    {
        public DbSet<TestEntity> TestEntities { get; set; } = null!;

        public TestModuleDbContext(DbContextOptions<TestModuleDbContext> options) : base(options)
        {
        }
    }

    private class FakeScopeContext : IScopeContext
    {
        public DefaultId? UserId { get; }
        public bool IsAuthenticated => UserId.HasValue;
        public IReadOnlyList<string> Roles { get; } = [];
        public IReadOnlyDictionary<string, string> Claims { get; } = new Dictionary<string, string>();
        public bool IsSystem => false;
        public string? CorrelationId { get; }

        public FakeScopeContext(DefaultId? userId, string? correlationId)
        {
            UserId = userId;
            CorrelationId = correlationId;
        }

        public string? GetClaim(string type) => null;
        public IReadOnlyList<string> GetClaimValues(string type) => [];
        public bool IsInRole(string role) => false;
    }

    #endregion
}
