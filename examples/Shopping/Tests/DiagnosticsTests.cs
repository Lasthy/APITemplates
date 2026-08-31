using System.Diagnostics;
using System.Net;
using System.Security.Claims;
using Kernel.Boxing;
using Kernel.Common;
using Kernel.Configuration;
using Kernel.Context;
using Kernel.Diagnostics;
using Kernel.Json;
using Kernel.Partitioning;
using Kernel.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Shopping.Tests;

public class DiagnosticsTests : IClassFixture<ShoppingApiTestFactory>
{
    private readonly ShoppingApiTestFactory _factory;

    public DiagnosticsTests(ShoppingApiTestFactory factory)
    {
        _factory = factory;
    }

    #region CorrelationIdMiddleware Tests

    [Fact]
    public async Task CorrelationIdMiddleware_WhenHeaderPresent_PreservesCorrelationId()
    {
        // Arrange
        var expectedCorrelationId = "custom-corr-id-12345";
        var context = new DefaultHttpContext();
        context.Request.Headers[DiagnosticsConstants.CorrelationIdHeader] = expectedCorrelationId;

        var invoked = false;
        var middleware = new CorrelationIdMiddleware(ctx =>
        {
            invoked = true;
            return Task.CompletedTask;
        });

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.True(invoked);
        Assert.Equal(expectedCorrelationId, context.Items[DiagnosticsConstants.CorrelationIdItemKey]);
        Assert.Equal(expectedCorrelationId, context.Response.Headers[DiagnosticsConstants.CorrelationIdHeader].ToString());
    }

    [Fact]
    public async Task CorrelationIdMiddleware_WhenHeaderAbsent_GeneratesValidUlid()
    {
        // Arrange
        var context = new DefaultHttpContext();

        var invoked = false;
        var middleware = new CorrelationIdMiddleware(ctx =>
        {
            invoked = true;
            return Task.CompletedTask;
        });

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.True(invoked);
        var generatedId = context.Items[DiagnosticsConstants.CorrelationIdItemKey] as string;
        Assert.NotNull(generatedId);
        Assert.True(DefaultId.TryParse(generatedId, out _));
        Assert.Equal(generatedId, context.Response.Headers[DiagnosticsConstants.CorrelationIdHeader].ToString());
    }

    [Fact]
    public async Task CorrelationIdMiddleware_WhenHeaderIsWhitespace_GeneratesValidUlid()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Headers[DiagnosticsConstants.CorrelationIdHeader] = "   ";

        var middleware = new CorrelationIdMiddleware(ctx => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var generatedId = context.Items[DiagnosticsConstants.CorrelationIdItemKey] as string;
        Assert.NotNull(generatedId);
        Assert.True(DefaultId.TryParse(generatedId, out _));
    }

    [Fact]
    public void CorrelationIdMiddleware_NullNext_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new CorrelationIdMiddleware(null!));
    }

    [Fact]
    public void RequestDiagnosticsMiddleware_NullNextOrLogger_ThrowsArgumentNullException()
    {
        var logger = new TestLogger<RequestDiagnosticsMiddleware>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new RequestDiagnosticsMiddleware(null!, logger));
        Assert.Throws<ArgumentNullException>(() => new RequestDiagnosticsMiddleware(ctx => Task.CompletedTask, null!));
    }

    #endregion

    #region RequestDiagnosticsMiddleware Tests

    [Fact]
    public async Task RequestDiagnosticsMiddleware_LogsRequestDetails()
    {
        // Arrange
        var logger = new TestLogger<RequestDiagnosticsMiddleware>();
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/api/v1/catalog/products";
        context.Response.StatusCode = 200;
        context.Items[DiagnosticsConstants.CorrelationIdItemKey] = "corr-test-log-1";

        var middleware = new RequestDiagnosticsMiddleware(ctx => Task.CompletedTask, logger);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Single(logger.Logs);
        var entry = logger.Logs[0];
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Contains("HTTP GET /api/v1/catalog/products responded 200", entry.Message);
        Assert.Contains("corr-test-log-1", entry.Message);
    }

    [Fact]
    public async Task RequestDiagnosticsMiddleware_LogsAuthenticatedUser()
    {
        // Arrange
        var logger = new TestLogger<RequestDiagnosticsMiddleware>();
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/api/v1/orders";
        context.Response.StatusCode = 201;

        var userId = DefaultId.NewUlid().ToString();
        var claims = new[] { new Claim("sub", userId) };
        context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));

        var middleware = new RequestDiagnosticsMiddleware(ctx => Task.CompletedTask, logger);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Single(logger.Logs);
        var entry = logger.Logs[0];
        Assert.Contains($"[User={userId}]", entry.Message);
    }

    [Fact]
    public async Task RequestDiagnosticsMiddleware_OnException_LogsAndRethrows()
    {
        // Arrange
        var logger = new TestLogger<RequestDiagnosticsMiddleware>();
        var context = new DefaultHttpContext();
        context.Request.Method = "DELETE";
        context.Request.Path = "/api/v1/catalog/products/123";

        var middleware = new RequestDiagnosticsMiddleware(
            ctx => throw new InvalidOperationException("Downstream error"),
            logger);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(context));

        // Diagnostics should still be logged in finally block
        Assert.Single(logger.Logs);
        var entry = logger.Logs[0];
        Assert.Contains("HTTP DELETE /api/v1/catalog/products/123", entry.Message);
    }

    #endregion

    #region IScopeContext CorrelationId Tests

    [Fact]
    public void WebScopeContext_ReadsCorrelationIdFromHttpContextItems()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Items[DiagnosticsConstants.CorrelationIdItemKey] = "corr-scope-items-123";

        var accessor = new TestHttpContextAccessor { HttpContext = context };
        var scopeContext = new WebScopeContext(accessor);

        // Act & Assert
        Assert.Equal("corr-scope-items-123", scopeContext.CorrelationId);
    }

    [Fact]
    public void WebScopeContext_ReadsCorrelationIdFromHeader_WhenNotInItems()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Headers[DiagnosticsConstants.CorrelationIdHeader] = "corr-header-direct-456";

        var accessor = new TestHttpContextAccessor { HttpContext = context };
        var scopeContext = new WebScopeContext(accessor);

        // Act & Assert
        Assert.Equal("corr-header-direct-456", scopeContext.CorrelationId);
    }

    [Fact]
    public void WebScopeContext_WithExplicitCorrelationId_ReturnsExplicitValue()
    {
        // Arrange
        var derivedContext = new TestScopeContext("explicit-worker-corr-789");

        // Act & Assert
        Assert.Equal("explicit-worker-corr-789", derivedContext.CorrelationId);
    }

    private class TestScopeContext : WebScopeContext
    {
        public TestScopeContext(string correlationId) : base(null, correlationId) { }
    }

    #endregion

    #region MessageWriter CorrelationId Tests

    [Fact]
    public async Task MessageWriter_PersistsCorrelationId_FromScopeContext()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<KernelDbContext>()
            .UseInMemoryDatabase(databaseName: $"Diagnostics_Db_{Guid.NewGuid()}")
            .Options;

        using var db = new KernelDbContext(options);
        var jsonSerializer = new JsonSerializer();
        var partitionManager = new PartitionManager(new ApplicationSettings(jsonSerializer));

        var scopeContext = new CustomTestScopeContext { CorrelationId = "corr-writer-persisted-999" };
        var writer = new MessageWriter(jsonSerializer, partitionManager, db, scopeContext);

        // Act
        var result = await writer.WriteAsync(new { Test = "Payload" }, MessageChannel.Event);

        // Assert
        Assert.True(result.IsSuccess);
        var savedMessage = await db.Messages.FirstOrDefaultAsync();
        Assert.NotNull(savedMessage);
        Assert.Equal("corr-writer-persisted-999", savedMessage.CorrelationId);
    }

    [Fact]
    public async Task MessageWriter_WhenNoCorrelationId_PersistsNullCorrelationId()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<KernelDbContext>()
            .UseInMemoryDatabase(databaseName: $"Diagnostics_Db_Null_{Guid.NewGuid()}")
            .Options;

        using var db = new KernelDbContext(options);
        var jsonSerializer = new JsonSerializer();
        var partitionManager = new PartitionManager(new ApplicationSettings(jsonSerializer));

        var writer = new MessageWriter(jsonSerializer, partitionManager, db, null);

        // Act
        var result = await writer.WriteAsync(new { Test = "PayloadWithoutCorrId" }, MessageChannel.Event);

        // Assert
        Assert.True(result.IsSuccess);
        var savedMessage = await db.Messages.FirstOrDefaultAsync();
        Assert.NotNull(savedMessage);
        Assert.Null(savedMessage.CorrelationId);
    }

    #endregion

    #region End-to-End HTTP Integration Tests

    [Fact]
    public async Task HttpPipeline_WhenCustomCorrelationIdProvided_ReturnsSameCorrelationIdInResponseHeader()
    {
        // Arrange
        var client = _factory.CreateClient();
        var expectedCorrId = "client-e2e-corr-id-" + Guid.NewGuid();
        client.DefaultRequestHeaders.Add(DiagnosticsConstants.CorrelationIdHeader, expectedCorrId);

        // Act
        var response = await client.GetAsync("/api/v1/kernel/health/live");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains(DiagnosticsConstants.CorrelationIdHeader));
        var returnedCorrId = response.Headers.GetValues(DiagnosticsConstants.CorrelationIdHeader).FirstOrDefault();
        Assert.Equal(expectedCorrId, returnedCorrId);
    }

    [Fact]
    public async Task HttpPipeline_WhenNoCorrelationIdProvided_GeneratesAndReturnsUlidInResponseHeader()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/v1/kernel/health/live");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains(DiagnosticsConstants.CorrelationIdHeader));
        var returnedCorrId = response.Headers.GetValues(DiagnosticsConstants.CorrelationIdHeader).FirstOrDefault();
        Assert.NotNull(returnedCorrId);
        Assert.True(DefaultId.TryParse(returnedCorrId, out _));
    }

    #endregion

    #region Test Helpers

    private class TestLogger<T> : ILogger<T>
    {
        public List<LogEntry> Logs { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Logs.Add(new LogEntry(logLevel, formatter(state, exception), exception));
        }
    }

    private record LogEntry(LogLevel Level, string Message, Exception? Exception);

    private class TestHttpContextAccessor : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; }
    }

    private class CustomTestScopeContext : IScopeContext
    {
        public DefaultId? UserId { get; set; }
        public bool IsAuthenticated { get; set; }
        public IReadOnlyList<string> Roles { get; set; } = [];
        public IReadOnlyDictionary<string, string> Claims { get; set; } = new Dictionary<string, string>();
        public bool IsSystem => false;
        public string? CorrelationId { get; set; }

        public string? GetClaim(string claimType) => null;
        public IReadOnlyList<string> GetClaimValues(string claimType) => [];
        public bool IsInRole(string role) => false;
    }

    #endregion
}
