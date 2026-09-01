using Kernel.AuditLog;
using Kernel.AuditLog.Features;
using Kernel.Authorization.Extensions;
using Kernel.Caching;
using Kernel.Common;
using Kernel.Configuration;
using Kernel.Configuration.Features;
using Kernel.Context;
using Kernel.Email;
using Kernel.Encryption;
using Kernel.Encryption.Extensions;
using Kernel.Events;
using Kernel.Health;
using Kernel.Health.Checks;
using Kernel.Health.Features;
using Kernel.Json;
using Kernel.Messaging;
using Kernel.Notifications;
using Kernel.Partitioning;
using Kernel.Persistence;
using Kernel.Persistence.Extensions;
using Kernel.Requests;
using Kernel.Storage;
using Kernel.Testing.Fakes;
using Kernel.Validation;
using Kernel.Validation.Extensions;
using Kernel.Versioning;
using Kernel.Versioning.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Kernel.Testing.Infrastructure;

/// <summary>
/// Base test fixture providing an isolated, in-memory DI container, database, and pre-configured fakes
/// for testing Kernel services, handlers, and interceptors.
/// </summary>
public abstract class KernelTestBase : IAsyncLifetime, IDisposable
{
    private IServiceProvider? _serviceProvider;
    private IServiceScope? _scope;
    private readonly Lazy<string> _databaseName;

    /// <summary>
    /// The unique in-memory database name used by this test instance.
    /// </summary>
    public string DatabaseName => _databaseName.Value;

    /// <summary>
    /// The test fake event bus. Captures all events published during test execution.
    /// </summary>
    public FakeEventBus EventBus { get; } = new();

    /// <summary>
    /// The test fake scope context. Allows configuring user ID, roles, claims, and correlation ID.
    /// </summary>
    public FakeScopeContext ScopeContext { get; } = new();

    /// <summary>
    /// The test fake email sender. Captures all sent emails and allows simulating failures.
    /// </summary>
    public FakeEmailSender EmailSender { get; } = new();

    /// <summary>
    /// The test fake cache service.
    /// </summary>
    public FakeCacheService CacheService { get; } = new();

    /// <summary>
    /// The test fake encryption service.
    /// </summary>
    public FakeEncryptionService EncryptionService { get; } = new();

    /// <summary>
    /// The test fake file storage service.
    /// </summary>
    public FakeFileStorage FileStorage { get; } = new();

    /// <summary>
    /// The test fake notification service.
    /// </summary>
    public FakeNotificationService NotificationService { get; } = new();

    /// <summary>
    /// The centralized JSON serializer.
    /// </summary>
    public IJsonSerializer JsonSerializer { get; } = new JsonSerializer();

    /// <summary>
    /// The dynamic settings registry.
    /// </summary>
    public DynamicSettings DynamicSettings { get; }

    /// <summary>
    /// The test configuration.
    /// </summary>
    public IConfiguration Configuration { get; }

    /// <summary>
    /// The root service provider for this test instance.
    /// </summary>
    public IServiceProvider Services => _serviceProvider ??= BuildServiceProvider();

    /// <summary>
    /// The request dispatcher.
    /// </summary>
    public IDispatcher Dispatcher => Services.GetRequiredService<IDispatcher>();

    /// <summary>
    /// The in-memory KernelDbContext.
    /// </summary>
    public KernelDbContext KernelDbContext => Services.GetRequiredService<KernelDbContext>();

    protected KernelTestBase()
    {
        _databaseName = new Lazy<string>(() => $"KernelTestDb_{Guid.NewGuid():N}");

        DynamicSettings = new DynamicSettings(JsonSerializer, "Testing");

        var hostSettings = new HostSettings(JsonSerializer, "Testing") { AutoSave = false };
        var applicationSettings = new ApplicationSettings(JsonSerializer, "Testing") { AutoSave = false };
        var instanceSettings = new InstanceSettings(JsonSerializer, "Testing") { AutoSave = false };
        var messagingSettings = new MessagingSettings(JsonSerializer, "Testing") { AutoSave = false };
        var emailSettings = new EmailSettings(JsonSerializer, "Testing") { AutoSave = false };
        var cacheSettings = new CacheSettings(JsonSerializer, "Testing") { AutoSave = false };
        var encryptionSettings = new EncryptionSettings(JsonSerializer, "Testing") { AutoSave = false };
        var versioningSettings = new VersioningSettings(JsonSerializer, "Testing") { AutoSave = false };
        var fileStorageSettings = new FileStorageSettings(JsonSerializer, "Testing") { AutoSave = false };
        var notificationSettings = new NotificationSettings(JsonSerializer, "Testing") { AutoSave = false };
        var dataSettings = new DataSettings(JsonSerializer, "Testing") { AutoSave = false };
        dataSettings.SetValue("Provider", "InMemory");
        dataSettings.SetValue("ConnectionString", DatabaseName);

        DynamicSettings.AddSection(hostSettings);
        DynamicSettings.AddSection(applicationSettings);
        DynamicSettings.AddSection(instanceSettings);
        DynamicSettings.AddSection(messagingSettings);
        DynamicSettings.AddSection(emailSettings);
        DynamicSettings.AddSection(cacheSettings);
        DynamicSettings.AddSection(encryptionSettings);
        DynamicSettings.AddSection(versioningSettings);
        DynamicSettings.AddSection(fileStorageSettings);
        DynamicSettings.AddSection(notificationSettings);
        DynamicSettings.AddSection(dataSettings);

        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddDynamicSettings(DynamicSettings);
        Configuration = configBuilder.Build();
    }

    /// <summary>
    /// Override to register additional test services or customize the DI container before it is built.
    /// </summary>
    protected virtual void ConfigureServices(IServiceCollection services)
    {
        // ── Serialization ─────────────────────────────────────────────────────
        services.AddSingleton<IJsonSerializer>(JsonSerializer);

        // ── Logging ───────────────────────────────────────────────────────────
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        // ── Fakes & Scope Context ─────────────────────────────────────────────
        services.AddSingleton(ScopeContext);
        services.AddSingleton<IScopeContext>(ScopeContext);

        services.AddSingleton(EventBus);
        services.AddSingleton<IEventBus>(EventBus);
        services.AddSingleton<IEventTypeRegistry, EventTypeRegistry>();

        services.AddSingleton(EmailSender);
        services.AddSingleton<IEmailSender>(EmailSender);
        services.AddSingleton<IEmailTemplateRenderer, DefaultEmailTemplateRenderer>();

        services.AddSingleton(CacheService);
        services.AddSingleton<ICacheService>(CacheService);

        services.AddSingleton(EncryptionService);
        services.AddSingleton<IEncryptionService>(EncryptionService);

        services.AddSingleton(FileStorage);
        services.AddSingleton<IFileStorage>(FileStorage);

        services.AddSingleton(NotificationService);
        services.AddSingleton<INotificationService>(NotificationService);

        // ── Dynamic Settings & Configuration ──────────────────────────────────
        services.AddSingleton<IConfiguration>(Configuration);
        services.AddSingleton(DynamicSettings);
        services.AddSingleton(DynamicSettings.GetSection<HostSettings>());
        services.AddSingleton(DynamicSettings.GetSection<ApplicationSettings>());
        services.AddSingleton(DynamicSettings.GetSection<InstanceSettings>());
        services.AddSingleton(DynamicSettings.GetSection<MessagingSettings>());
        services.AddSingleton(DynamicSettings.GetSection<EmailSettings>());
        services.AddSingleton(DynamicSettings.GetSection<CacheSettings>());
        services.AddSingleton(DynamicSettings.GetSection<EncryptionSettings>());
        services.AddSingleton(DynamicSettings.GetSection<VersioningSettings>());
        services.AddSingleton(DynamicSettings.GetSection<FileStorageSettings>());
        services.AddSingleton(DynamicSettings.GetSection<NotificationSettings>());
        services.AddSingleton(DynamicSettings.GetSection<DataSettings>());

        // ── API Versioning ────────────────────────────────────────────────────
        services.AddKernelVersioning(DynamicSettings.GetSection<VersioningSettings>());

        // ── Partition Manager ─────────────────────────────────────────────────
        services.AddSingleton<IPartitionManager, PartitionManager>();

        // ── Authorization & Validation ────────────────────────────────────────
        services.AddAuthorizationPolicies(typeof(KernelTestBase).Assembly);
        services.AddAuthorizationPolicies(typeof(KernelDbContext).Assembly);
        services.AddValidation(typeof(KernelDbContext).Assembly);

        // ── Health ────────────────────────────────────────────────────────────
        var healthCheckRegistry = new HealthCheckRegistry();
        healthCheckRegistry.Register<MemoryHealthCheck>("Kernel");
        services.AddSingleton<IHealthCheckRegistry>(healthCheckRegistry);
        services.AddScoped<IHealthService, HealthService>();
        services.AddScoped<MemoryHealthCheck>();

        // ── Audit Log ─────────────────────────────────────────────────────────
        services.AddScoped<IAuditLogService, AuditLogService>();

        // ── Messaging / Outbox / Inbox ────────────────────────────────────────
        services.AddScoped<IInbox, Inbox>();
        services.AddScoped<IMessageWriter, MessageWriter>();
        services.AddScoped<IMessageDispatcher, MessageDispatcher>();
        services.AddScoped<IMessageProcessor, EventMessageProcessor>();
        services.AddScoped<IMessageProcessor, EmailMessageProcessor>();

        // ── Dispatcher & Interceptors ─────────────────────────────────────────
        services.AddScoped<IDispatcher, Dispatcher>();
        services.AddTransient<CacheInterceptor>();
        services.AddTransient<ValidationInterceptor>();
        services.AddTransient<LoggingInterceptor>();
        services.AddTransient<TransactionInterceptor>();

        services.AddScoped<IRequestHandler<GetSettings, string>, GetSettingsHandler>();
        services.AddScoped<IRequestHandler<UpdateSettings, bool>, UpdateSettingsHandler>();
        services.AddScoped<IRequestHandler<GetHealthReport, HealthReport>, GetHealthReportHandler>();
        services.AddScoped<IRequestHandler<GetModuleHealthReport, ModuleHealthReport>, GetModuleHealthReportHandler>();
        services.AddScoped<IRequestHandler<GetAuditEntries, PaginatedResult<AuditEntry>>, GetAuditEntriesHandler>();

        // ── Persistence Interceptors ──────────────────────────────────────────
        services.AddScoped<ISaveChangesInterceptor, SoftDeleteSaveChangesInterceptor>();
        services.AddScoped<ISaveChangesInterceptor, AuditSaveChangesInterceptor>();
        services.AddScoped<ISaveChangesInterceptor, AuditLogSaveChangesInterceptor>();
        services.AddScoped<ISaveChangesInterceptor, DispatchDomainEventsSaveChangesInterceptor>();
        services.AddScoped<SoftDeleteSaveChangesInterceptor>();
        services.AddScoped<AuditSaveChangesInterceptor>();
        services.AddScoped<AuditLogSaveChangesInterceptor>();
        services.AddScoped<DispatchDomainEventsSaveChangesInterceptor>();

        // ── Kernel DbContext ──────────────────────────────────────────────────
        services.AddDbContext<KernelDbContext>((sp, options) =>
        {
            options.UseInMemoryDatabase(DatabaseName);
            options.AddKernelInterceptors(sp);
        });

        var dbContextRegistry = new DbContextTypeRegistry();
        dbContextRegistry.Register<KernelDbContext>();
        services.AddSingleton(dbContextRegistry);
    }

    private IServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        var provider = services.BuildServiceProvider();

        // Ensure database created
        using var scope = provider.CreateScope();
        var kernelDb = scope.ServiceProvider.GetRequiredService<KernelDbContext>();
        kernelDb.Database.EnsureCreated();

        return provider;
    }

    /// <summary>
    /// Creates a new DI scope from the test service provider.
    /// </summary>
    public IServiceScope CreateScope()
    {
        return Services.CreateScope();
    }

    /// <summary>
    /// Executes an asynchronous delegate inside a fresh DI scope.
    /// </summary>
    public async Task ExecuteScopeAsync(Func<IServiceProvider, Task> action)
    {
        using var scope = CreateScope();
        await action(scope.ServiceProvider);
    }

    /// <summary>
    /// Executes an asynchronous function returning a result inside a fresh DI scope.
    /// </summary>
    public async Task<TResult> ExecuteScopeAsync<TResult>(Func<IServiceProvider, Task<TResult>> action)
    {
        using var scope = CreateScope();
        return await action(scope.ServiceProvider);
    }

    /// <summary>
    /// Executes an asynchronous delegate on a resolved DbContext inside a fresh DI scope.
    /// </summary>
    public async Task ExecuteDbContextAsync<TDbContext>(Func<TDbContext, Task> action)
        where TDbContext : DbContext
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TDbContext>();
        await action(db);
    }

    /// <summary>
    /// Executes an asynchronous function on a resolved DbContext inside a fresh DI scope.
    /// </summary>
    public async Task<TResult> ExecuteDbContextAsync<TDbContext, TResult>(Func<TDbContext, Task<TResult>> action)
        where TDbContext : DbContext
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TDbContext>();
        return await action(db);
    }

    public virtual Task InitializeAsync()
    {
        _scope = CreateScope();
        return Task.CompletedTask;
    }

    public virtual Task DisposeAsync()
    {
        Dispose();
        return Task.CompletedTask;
    }

    public virtual void Dispose()
    {
        _scope?.Dispose();
        if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
        GC.SuppressFinalize(this);
    }
}
