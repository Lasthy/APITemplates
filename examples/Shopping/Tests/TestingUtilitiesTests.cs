using Catalog;
using Catalog.Features.Products;
using Catalog.Persistence;
using Identity;
using Identity.Contracts.Events;
using Identity.Features.Users;
using Identity.Persistence;
using Kernel.Common;
using Kernel.Events;
using Kernel.Email;
using Kernel.Testing.Assertions;
using Kernel.Testing.Fakes;
using Kernel.Testing.Infrastructure;
using Kernel.Validation;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Xunit.Sdk;

namespace Shopping.Tests;

public class TestingUtilitiesTests
{
    public record TestSampleEvent(string Message) : Event;
    public record AnotherSampleEvent(int Value) : Event;

    [Fact]
    public async Task FakeEventBus_CapturesEventsAndProvidesQueryHelpers()
    {
        var bus = new FakeEventBus();
        bus.ShouldBeEmpty();

        var evt1 = new TestSampleEvent("Hello");
        var evt2 = new TestSampleEvent("World");
        var evt3 = new AnotherSampleEvent(42);

        await bus.PublishAsync(evt1);
        await bus.PublishAsync(evt2);
        await bus.PublishAsync(evt3);

        Assert.Equal(3, bus.PublishedEvents.Count);
        Assert.Equal(2, bus.CountPublished<TestSampleEvent>());
        Assert.Equal(1, bus.CountPublished<AnotherSampleEvent>());

        Assert.True(bus.HasPublished<TestSampleEvent>());
        Assert.True(bus.HasPublished<TestSampleEvent>(e => e.Message == "World"));
        Assert.False(bus.HasPublished<TestSampleEvent>(e => e.Message == "NonExistent"));

        var lastSample = bus.GetLastPublishedEvent<TestSampleEvent>();
        Assert.NotNull(lastSample);
        Assert.Equal("World", lastSample.Message);

        var sampleList = bus.GetPublishedEvents<TestSampleEvent>();
        Assert.Equal(2, sampleList.Count);

        // Assertions
        bus.ShouldHavePublished<TestSampleEvent>();
        bus.ShouldHavePublished<TestSampleEvent>(e => e.Message == "Hello");
        bus.ShouldHavePublished<TestSampleEvent>(2);
        bus.ShouldNotHavePublished<TestSampleEvent>(e => e.Message == "Missing");

        // Assertion failures
        Assert.ThrowsAny<XunitException>(() => bus.ShouldBeEmpty());
        Assert.ThrowsAny<XunitException>(() => bus.ShouldHavePublished<TestSampleEvent>(5));
        Assert.ThrowsAny<XunitException>(() => bus.ShouldNotHavePublished<TestSampleEvent>());

        bus.Clear();
        bus.ShouldBeEmpty();
    }

    [Fact]
    public void FakeScopeContext_ProvidesFluentTestIdentityConfiguration()
    {
        var context = new FakeScopeContext();

        // Default anonymous
        Assert.False(context.IsAuthenticated);
        Assert.Null(context.UserId);
        Assert.False(context.IsSystem);

        // AsAuthenticated
        var userId = DefaultId.NewUlid();
        context.AsAuthenticated(userId, "Manager", "Editor");

        Assert.True(context.IsAuthenticated);
        Assert.Equal(userId, context.UserId);
        Assert.True(context.IsInRole("Manager"));
        Assert.True(context.IsInRole("Editor"));
        Assert.False(context.IsInRole("Admin"));
        Assert.False(context.IsSystem);

        // AsSystem
        context.AsSystem();
        Assert.True(context.IsAuthenticated);
        Assert.True(context.IsSystem);
        Assert.True(context.IsInRole("System"));

        // WithClaim & CorrelationId
        context.WithClaim("tenant_id", "tenant-123")
               .WithCorrelationId("corr-999");

        Assert.Equal("tenant-123", context.GetClaim("tenant_id"));
        Assert.Equal("corr-999", context.CorrelationId);
        Assert.Single(context.GetClaimValues("tenant_id"));

        // Reset
        context.Reset();
        Assert.False(context.IsAuthenticated);
        Assert.Null(context.UserId);
        Assert.Null(context.CorrelationId);
    }

    [Fact]
    public async Task FakeEmailSender_CapturesSentEmailsAndSimulatesFailures()
    {
        var sender = new FakeEmailSender();
        sender.ShouldNotHaveSent();

        var email1 = new EmailMessage
        {
            To = "user1@example.com",
            Subject = "Welcome",
            Body = "<p>Welcome!</p>"
        };

        var email2 = new EmailMessage
        {
            To = "user2@example.com",
            Cc = "admin@example.com",
            Subject = "Alert",
            Body = "<p>Alert!</p>"
        };

        var result1 = await sender.SendAsync(email1);
        result1.ShouldBeSuccess();

        var result2 = await sender.SendAsync(email2);
        result2.ShouldBeSuccess();

        Assert.Equal(2, sender.SentCount);
        sender.ShouldHaveSent();
        sender.ShouldHaveSentCount(2);
        sender.ShouldHaveSentTo("user1@example.com");
        sender.ShouldHaveSentTo("admin@example.com");
        sender.ShouldHaveSent(e => e.Subject == "Welcome");
        sender.ShouldNotHaveSent(e => e.Subject == "Unknown");

        // Assertion failures
        Assert.ThrowsAny<XunitException>(() => sender.ShouldNotHaveSent());
        Assert.ThrowsAny<XunitException>(() => sender.ShouldHaveSentTo("nonexistent@example.com"));

        // Failure simulation
        sender.ShouldSucceed = false;
        sender.FailureError = new TimeoutException("SMTP timeout");

        var email3 = new EmailMessage
        {
            To = "user3@example.com",
            Subject = "Fail",
            Body = "<p>Fail</p>"
        };

        var result3 = await sender.SendAsync(email3);
        result3.ShouldBeFailure<TimeoutException>();

        sender.Clear();
        sender.ShouldNotHaveSent();
    }

    [Fact]
    public async Task FakeCacheService_SupportsOperationsAndInspection()
    {
        var cache = new FakeCacheService();
        cache.ShouldBeEmpty();

        await cache.SetAsync("user:1", "Alice", TimeSpan.FromMinutes(10));
        await cache.SetAsync("user:2", "Bob", TimeSpan.FromMinutes(10));
        await cache.SetAsync("product:100", "Keyboard", TimeSpan.FromMinutes(10));

        cache.ShouldContainKey("user:1");
        cache.ShouldContain("user:1", "Alice");
        cache.ShouldHaveCount(3);

        // Assertion failures
        Assert.ThrowsAny<XunitException>(() => cache.ShouldNotContainKey("user:1"));
        Assert.ThrowsAny<XunitException>(() => cache.ShouldContainKey("user:999"));
        Assert.ThrowsAny<XunitException>(() => cache.ShouldContain("user:1", "WrongValue"));
        Assert.ThrowsAny<XunitException>(() => cache.ShouldBeEmpty());

        var val = await cache.GetAsync<string>("user:1");
        Assert.Equal("Alice", val);

        // GetOrCreate
        var created = await cache.GetOrCreateAsync("user:3", () => Task.FromResult("Charlie"));
        Assert.Equal("Charlie", created);
        cache.ShouldContain("user:3", "Charlie");

        // RemoveByPrefix
        await cache.RemoveByPrefixAsync("user:");
        cache.ShouldNotContainKey("user:1");
        cache.ShouldNotContainKey("user:2");
        cache.ShouldNotContainKey("user:3");
        cache.ShouldContainKey("product:100");

        // Remove
        await cache.RemoveAsync("product:100");
        cache.ShouldBeEmpty();
    }

    [Fact]
    public void ResultAssertions_ValidatesSuccessAndFailureScenarios()
    {
        // Success non-generic
        var successResult = Result.Success();
        successResult.ShouldBeSuccess();
        Assert.ThrowsAny<XunitException>(() => successResult.ShouldBeFailure());

        // Failure non-generic
        var failureResult = Result.Failure(new InvalidOperationException("Operation failed"));
        failureResult.ShouldBeFailure();
        var ex = failureResult.ShouldBeFailure<InvalidOperationException>();
        Assert.Equal("Operation failed", ex.Message);
        Assert.ThrowsAny<XunitException>(() => failureResult.ShouldBeSuccess());

        // Success generic
        var genericSuccess = Result.Success<string>("Sample Value");
        var value = genericSuccess.ShouldBeSuccess();
        Assert.Equal("Sample Value", value);

        // Failure generic
        var genericFailure = Result.Failure<string>(new UnauthorizedAccessException("Access denied"));
        var authEx = genericFailure.ShouldBeFailure<string, UnauthorizedAccessException>();
        Assert.Equal("Access denied", authEx.Message);

        // Validation Error
        var validationErrors = new[] { new ValidationError("Email", "Invalid email address.") };
        var validationFailure = Result.Failure(new ValidationException(ValidationResult.Failure(validationErrors)));
        var valEx = validationFailure.ShouldHaveValidationError("Email");
        Assert.Single(valEx.Errors);

        // Paginated result
        var paginated = PaginatedResult<string>.Create(["Item1", "Item2"], page: 1, pageSize: 10, totalCount: 2);
        paginated.ShouldHavePage(1, 10, 2);
    }
}

public class CatalogModuleTests : ModuleTestBase<CatalogModule, CatalogDbContext>
{
    [Fact]
    public async Task ModuleTestBase_ExecutesHandlersAndQueriesIsolatedInMemoryDb()
    {
        // ScopeContext configuration
        ScopeContext.AsSystem();

        // Dispatch CreateProduct command
        var createResult = await Dispatcher.DispatchAsync(new CreateProduct(
            Name: "Test Gaming Mouse",
            Description: "High precision mouse",
            Sku: "TEST-MOUSE",
            Price: 49.99m,
            StockQuantity: 100));

        var productDto = createResult.ShouldBeSuccess();
        Assert.Equal("Test Gaming Mouse", productDto.Name);
        Assert.Equal("TEST-MOUSE", productDto.Sku);

        // Query DbContext directly
        var productInDb = await DbContext.Products.FindAsync(productDto.Id);
        Assert.NotNull(productInDb);
        Assert.Equal(49.99m, productInDb.Price);

        // Cache fake inspection
        CacheService.ShouldNotContainKey("non-existent");
    }
}

public class IdentityModuleTests : ModuleTestBase<IdentityModule, IdentityModuleDbContext>
{
    [Fact]
    public async Task ModuleTestBase_CreateUser_DispatchesAndPublishesEvent()
    {
        ScopeContext.AsSystem();

        var result = await Dispatcher.DispatchAsync(new CreateUser(
            Email: "developer@example.com",
            UserName: "devuser",
            Password: "Password123!",
            ProfileName: "Client"));

        var userDto = result.ShouldBeSuccess();
        Assert.Equal("developer@example.com", userDto.Email);
        Assert.Equal("devuser", userDto.UserName);

        // Verify EventBus recorded UserCreatedEvent
        EventBus.ShouldHavePublished<UserCreatedEvent>(e => e.Email == "developer@example.com");

        // Verify user exists in DbContext
        var userInDb = await DbContext.Users.FindAsync(userDto.Id);
        Assert.NotNull(userInDb);
        Assert.Equal("devuser", userInDb.UserName);
    }
}

public class TestingWebApplicationFactoryTests : IClassFixture<TestWebApplicationFactory<Program>>
{
    private readonly TestWebApplicationFactory<Program> _factory;

    public TestingWebApplicationFactoryTests(TestWebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task TestWebApplicationFactory_StartsHostWithInMemoryDbAndFakes()
    {
        var client = _factory.CreateClient();

        // Live probe
        var liveResponse = await client.GetAsync("/api/v1/kernel/health/live");
        Assert.True(liveResponse.IsSuccessStatusCode);

        // Authenticated client with TestScheme
        var authClient = _factory.CreateAuthenticatedClient(DefaultId.NewUlid(), "System");
        var reportResponse = await authClient.GetAsync("/api/v1/kernel/health/report");
        Assert.True(reportResponse.IsSuccessStatusCode);
    }
}
