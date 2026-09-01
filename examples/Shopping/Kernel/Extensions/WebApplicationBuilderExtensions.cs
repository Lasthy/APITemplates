using Kernel.AuditLog;
using Kernel.AuditLog.Controllers;
using Kernel.AuditLog.Features;
using Kernel.Authorization;
using Kernel.Authorization.Extensions;
using Kernel.Boxing;
using Kernel.Caching;
using Kernel.Caching.Extensions;
using Kernel.Common;
using Kernel.Configuration;
using Kernel.Configuration.Controllers;
using Kernel.Configuration.Features;
using Kernel.Context;
using Kernel.Diagnostics;
using Kernel.Diagnostics.Extensions;
using Kernel.Email;
using Kernel.Encryption;
using Kernel.Encryption.Extensions;
using Kernel.Events;
using Kernel.Health;
using Kernel.Health.Checks;
using Kernel.Health.Controllers;
using Kernel.Health.Features;
using Kernel.Json;
using Kernel.Messaging;
using Kernel.Messaging.Workers;
using Kernel.Modules;
using Kernel.Partitioning;
using Kernel.Persistence;
using Kernel.Persistence.Extensions;
using Kernel.RateLimiting;
using Kernel.RateLimiting.Extensions;
using Kernel.Requests;
using Kernel.Validation;
using Kernel.Validation.Extensions;
using Kernel.Versioning;
using Kernel.Versioning.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Kernel.Extensions;

/// <summary>
/// Extension methods for wiring up the Kernel and Data infrastructure on <see cref="WebApplicationBuilder"/>
/// and <see cref="WebApplication"/>.
/// </summary>
public static class WebApplicationBuilderExtensions
{
    /// <summary>
    /// Registers all Kernel services into the DI container and adds the kernel OpenAPI document.
    /// Call this before <see cref="WebApplicationBuilderExtension.AddModules"/> in <c>Program.cs</c>.
    /// </summary>
    public static void AddKernel(this WebApplicationBuilder builder)
    {
        // ── JSON serializer ───────────────────────────────────────────────────
        builder.Services.AddSingleton<IJsonSerializer, JsonSerializer>();

        // ── HTTP context / scope context ──────────────────────────────────────
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<IScopeContext, WebScopeContext>();

        // ── Authorization infrastructure ──────────────────────────────────────
        builder.Services.AddAuthorizationPolicies(typeof(WebApplicationBuilderExtensions).Assembly);

        // ── Validation infrastructure ─────────────────────────────────────────
        builder.Services.AddValidation(typeof(WebApplicationBuilderExtensions).Assembly);

        // ── Event infrastructure ──────────────────────────────────────────────
        builder.Services.AddSingleton<IEventBus, InProcessEventBus>();
        builder.Services.AddSingleton<IEventTypeRegistry, EventTypeRegistry>();
        builder.Services.AddSingleton(new ModuleRegistry());

        // ── Health check infrastructure ───────────────────────────────────────
        var healthCheckRegistry = new HealthCheckRegistry();
        healthCheckRegistry.Register<MemoryHealthCheck>("Kernel");
        builder.Services.AddSingleton<IHealthCheckRegistry>(healthCheckRegistry);
        builder.Services.AddScoped<IHealthService, HealthService>();
        builder.Services.AddScoped<MemoryHealthCheck>();

        // ── Audit Log infrastructure ──────────────────────────────────────────
        builder.Services.AddScoped<IAuditLogService, AuditLogService>();

        // ── Dynamic Settings ──────────────────────────────────────────────────
        var environment = builder.Environment.EnvironmentName;
        var jsonSerializer = new JsonSerializer();
        var dynamicSettings = new DynamicSettings(jsonSerializer, environment);

        // Register DynamicSettings into ASP.NET Configuration pipeline
        ((IConfigurationBuilder)builder.Configuration).AddDynamicSettings(dynamicSettings);

        // Register root host settings (appsettings.json, appsettings.{Environment}.json)
        var hostSettings = new HostSettings(jsonSerializer, environment);
        dynamicSettings.AddSection(hostSettings);

        // Register the core settings sections
        var applicationSettings = new ApplicationSettings(jsonSerializer, environment);
        dynamicSettings.AddSection(applicationSettings);

        var instanceSettings = new InstanceSettings(jsonSerializer, environment);
        dynamicSettings.AddSection(instanceSettings);

        var messagingSettings = new MessagingSettings(jsonSerializer, environment);
        dynamicSettings.AddSection(messagingSettings);

        var emailSettings = new EmailSettings(jsonSerializer, environment);
        dynamicSettings.AddSection(emailSettings);

        var cacheSettings = new CacheSettings(jsonSerializer, environment);
        dynamicSettings.AddSection(cacheSettings);

        var encryptionSettings = new EncryptionSettings(jsonSerializer, environment);
        dynamicSettings.AddSection(encryptionSettings);

        var versioningSettings = new VersioningSettings(jsonSerializer, environment);
        dynamicSettings.AddSection(versioningSettings);

        builder.Services.AddSingleton(dynamicSettings);
        builder.Services.AddSingleton(hostSettings);
        builder.Services.AddSingleton(applicationSettings);
        builder.Services.AddSingleton(instanceSettings);
        builder.Services.AddSingleton(messagingSettings);
        builder.Services.AddSingleton(emailSettings);
        builder.Services.AddSingleton(cacheSettings);
        builder.Services.AddSingleton(encryptionSettings);
        builder.Services.AddSingleton(versioningSettings);

        // ── API Versioning ────────────────────────────────────────────────────
        builder.AddKernelVersioning();

        // ── Caching infrastructure ────────────────────────────────────────────
        builder.Services.AddMemoryCache();
        builder.Services.AddSingleton<ICacheService, InMemoryCacheService>();

        // ── Encryption infrastructure ─────────────────────────────────────────
        builder.Services.AddSingleton<IEncryptionService, AesEncryptionService>();

        // ── Rate Limiting ─────────────────────────────────────────────────────
        builder.AddRateLimiting();

        // ── Email infrastructure ──────────────────────────────────────────────
        builder.Services.AddTransient<IEmailSender, SmtpEmailSender>();
        builder.Services.AddSingleton<IEmailTemplateRenderer, DefaultEmailTemplateRenderer>();

        // ── Partition manager ─────────────────────────────────────────────────
        builder.Services.AddSingleton<IPartitionManager, PartitionManager>();

        // ── Messaging / outbox / inbox ────────────────────────────────────────
        builder.Services.AddScoped<IInbox, Inbox>();
        builder.Services.AddScoped<IMessageWriter, MessageWriter>();
        builder.Services.AddScoped<IMessageDispatcher, MessageDispatcher>();
        builder.Services.AddScoped<IMessageProcessor, EventMessageProcessor>();
        builder.Services.AddScoped<IMessageProcessor, EmailMessageProcessor>();
        builder.Services.AddHostedService<MessageProcessingWorker>();
        builder.Services.AddHostedService<MessageCleanupWorker>();

        // ── Controllers ───────────────────────────────────────────────────────
        builder.Services.AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new Cysharp.Serialization.Json.UlidJsonConverter());
            })
            .AddApplicationPart(typeof(SettingsController).Assembly);

        builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
        {
            options.SerializerOptions.Converters.Add(new Cysharp.Serialization.Json.UlidJsonConverter());
        });

        // ── Configuration, Health & Audit Features ────────────────────────────
        builder.Services.AddScoped<IDispatcher, Dispatcher>();
        builder.Services.AddTransient<CacheInterceptor>();
        builder.Services.AddTransient<ValidationInterceptor>();
        builder.Services.AddTransient<LoggingInterceptor>();
        builder.Services.AddTransient<TransactionInterceptor>();
        builder.Services.AddScoped<IRequestHandler<GetSettings, string>, GetSettingsHandler>();
        builder.Services.AddScoped<IRequestHandler<UpdateSettings, bool>, UpdateSettingsHandler>();
        builder.Services.AddScoped<IRequestHandler<GetHealthReport, HealthReport>, GetHealthReportHandler>();
        builder.Services.AddScoped<IRequestHandler<GetModuleHealthReport, ModuleHealthReport>, GetModuleHealthReportHandler>();
        builder.Services.AddScoped<IRequestHandler<GetAuditEntries, PaginatedResult<AuditEntry>>, GetAuditEntriesHandler>();

        // ── Persistence Interceptors ──────────────────────────────────────────
        builder.Services.AddScoped<ISaveChangesInterceptor, SoftDeleteSaveChangesInterceptor>();
        builder.Services.AddScoped<ISaveChangesInterceptor, AuditSaveChangesInterceptor>();
        builder.Services.AddScoped<ISaveChangesInterceptor, AuditLogSaveChangesInterceptor>();
        builder.Services.AddScoped<ISaveChangesInterceptor, DispatchDomainEventsSaveChangesInterceptor>();
        builder.Services.AddScoped<SoftDeleteSaveChangesInterceptor>();
        builder.Services.AddScoped<AuditSaveChangesInterceptor>();
        builder.Services.AddScoped<AuditLogSaveChangesInterceptor>();
        builder.Services.AddScoped<DispatchDomainEventsSaveChangesInterceptor>();

        // ── OpenAPI — kernel document ─────────────────────────────────────────
        builder.Services.AddOpenApi("kernel", options =>
        {
            options.ShouldInclude = description =>
                string.Equals(description.GroupName, "kernel", StringComparison.OrdinalIgnoreCase);
        });
    }

    /// <summary>
    /// Registers the <see cref="DataSettings"/> dynamic section and the <see cref="KernelDbContext"/>
    /// configured with the database provider specified in <c>appsettings.Data.json</c>.
    /// </summary>
    public static void AddData(this WebApplicationBuilder builder)
    {
        var dynamicSettings = builder.Services
            .Where(d => d.ServiceType == typeof(DynamicSettings))
            .Select(d => d.ImplementationInstance as DynamicSettings)
            .FirstOrDefault();

        if (dynamicSettings == null)
        {
            throw new ApplicationException("DynamicSettings not found in DI. Did you call AddKernel() before AddData()?");
        }

        var jsonSerializer = builder.Services
            .Where(d => d.ServiceType == typeof(IJsonSerializer))
            .Select(d => d.ImplementationInstance as IJsonSerializer)
            .FirstOrDefault() ?? new JsonSerializer();

        var dataSettings = new DataSettings(jsonSerializer, dynamicSettings.Environment);
        dynamicSettings.AddSection(dataSettings);

        builder.Services.AddSingleton(dataSettings);

        builder.Services.AddDbContext<KernelDbContext>((sp, options) =>
        {
            options.UseConfiguredDatabase(dataSettings.Provider, dataSettings.ConnectionString, "kernel");
            options.AddKernelInterceptors(sp);
        });

        // Register DbContextTypeRegistry for automatic database and table initialization
        var dbContextRegistry = new DbContextTypeRegistry();
        dbContextRegistry.Register<KernelDbContext>();
        builder.Services.AddSingleton(dbContextRegistry);

        // Register Database Health Check
        builder.Services.AddScoped<DatabaseHealthCheck>();
        var healthCheckRegistry = builder.Services
            .Where(d => d.ServiceType == typeof(IHealthCheckRegistry))
            .Select(d => d.ImplementationInstance as IHealthCheckRegistry)
            .FirstOrDefault();

        healthCheckRegistry?.Register<DatabaseHealthCheck>("Data");
    }

    /// <summary>
    /// Configures the Kernel HTTP pipeline:
    /// maps the kernel OpenAPI JSON document at <c>/openapi/kernel.json</c>,
    /// maps controllers, and wires the <see cref="IEventBus"/> into
    /// <see cref="DynamicSettings"/> so that <see cref="SettingsChangedEvent"/>
    /// instances are published on value changes.
    /// </summary>
    public static void UseKernel(this WebApplication app)
    {
        // Wire the event bus into DynamicSettings now that the container is built.
        var dynamicSettings = app.Services.GetRequiredService<DynamicSettings>();
        var eventBus = app.Services.GetRequiredService<IEventBus>();
        dynamicSettings.SetEventBus(eventBus);

        // HTTP correlation ID and request diagnostics
        app.UseCorrelationId();
        app.UseRequestDiagnostics();

        // Expose the OpenAPI JSON documents globally. 
        app.MapOpenApi();

        app.UseRateLimiting();

        app.MapControllers();
    }

    /// <summary>
    /// Ensures that databases and tables for all registered DbContexts are automatically created on startup.
    /// Safe to call on existing databases (idempotent).
    /// </summary>
    public static void UseData(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var loggerFactory = scope.ServiceProvider.GetService<ILoggerFactory>();
        var logger = loggerFactory?.CreateLogger("Kernel.Data.Initialization");
        var registry = scope.ServiceProvider.GetService<DbContextTypeRegistry>();

        var contextTypes = new HashSet<Type>();
        if (registry != null)
        {
            foreach (var type in registry.Types)
            {
                contextTypes.Add(type);
            }
        }

        contextTypes.Add(typeof(KernelDbContext));

        foreach (var contextType in contextTypes)
        {
            try
            {
                if (scope.ServiceProvider.GetService(contextType) is DbContext dbContext)
                {
                    if (dbContext.Database.IsRelational())
                    {
                        var allMigrations = dbContext.Database.GetMigrations().ToList();
                        if (allMigrations.Count > 0)
                        {
                            var pendingMigrations = dbContext.Database.GetPendingMigrations().ToList();
                            dbContext.Database.Migrate();
                            if (pendingMigrations.Count > 0)
                            {
                                logger?.LogInformation("Applied {Count} pending migration(s) for {ContextType}: {Migrations}",
                                    pendingMigrations.Count, contextType.Name, string.Join(", ", pendingMigrations));
                            }
                            else
                            {
                                logger?.LogDebug("No pending migrations for {ContextType}", contextType.Name);
                            }
                        }
                        else
                        {
                            var creator = dbContext.Database.GetService<Microsoft.EntityFrameworkCore.Storage.IDatabaseCreator>()
                                as Microsoft.EntityFrameworkCore.Storage.RelationalDatabaseCreator;

                            if (creator != null)
                            {
                                if (!creator.Exists())
                                {
                                    creator.Create();
                                    logger?.LogInformation("Created database for {ContextType}", contextType.Name);
                                }

                                if (!ContextTablesExist(dbContext))
                                {
                                    try
                                    {
                                        creator.CreateTables();
                                        logger?.LogInformation("Created tables for {ContextType}", contextType.Name);
                                    }
                                    catch (Exception ex)
                                    {
                                        logger?.LogWarning(ex, "Could not create tables for {ContextType}", contextType.Name);
                                    }
                                }
                            }
                            else
                            {
                                dbContext.Database.EnsureCreated();
                            }
                        }
                    }
                    else
                    {
                        dbContext.Database.EnsureCreated();
                    }
                }
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Failed to initialize database tables for {ContextType}", contextType.Name);
            }
        }

        // Synchronously seed roles and default admin user if Identity module is present
        SeedIdentityData(scope.ServiceProvider, logger);
    }

    private static bool ContextTablesExist(DbContext context)
    {
        var entityTypes = context.Model.GetEntityTypes().ToList();
        if (!entityTypes.Any()) return true;

        var firstEntity = entityTypes.FirstOrDefault(e => !string.IsNullOrEmpty(e.GetTableName()));
        if (firstEntity == null) return true;

        var tableName = firstEntity.GetTableName()!;
        var schema = firstEntity.GetSchema();

        return TableExists(context, tableName, schema);
    }

    private static bool TableExists(DbContext context, string tableName, string? schema = null)
    {
        var connection = context.Database.GetDbConnection();
        var openedHere = false;
        if (connection.State != System.Data.ConnectionState.Open)
        {
            connection.Open();
            openedHere = true;
        }

        try
        {
            using var command = connection.CreateCommand();
            var provider = context.Database.ProviderName ?? string.Empty;

            if (provider.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
            {
                command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = @name;";
                var p = command.CreateParameter();
                p.ParameterName = "@name";
                p.Value = tableName;
                command.Parameters.Add(p);
                var result = Convert.ToInt64(command.ExecuteScalar());
                return result > 0;
            }
            else if (provider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) || provider.Contains("PostgreSQL", StringComparison.OrdinalIgnoreCase))
            {
                command.CommandText = "SELECT COUNT(*) FROM information_schema.tables WHERE LOWER(table_name) = LOWER(@name)" +
                    (string.IsNullOrEmpty(schema) ? ";" : " AND LOWER(table_schema) = LOWER(@schema);");
                var p = command.CreateParameter();
                p.ParameterName = "@name";
                p.Value = tableName;
                command.Parameters.Add(p);
                if (!string.IsNullOrEmpty(schema))
                {
                    var ps = command.CreateParameter();
                    ps.ParameterName = "@schema";
                    ps.Value = schema;
                    command.Parameters.Add(ps);
                }
                var result = Convert.ToInt64(command.ExecuteScalar());
                return result > 0;
            }
            else if (provider.Contains("SqlServer", StringComparison.OrdinalIgnoreCase))
            {
                command.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = @name" +
                    (string.IsNullOrEmpty(schema) ? ";" : " AND TABLE_SCHEMA = @schema;");
                var p = command.CreateParameter();
                p.ParameterName = "@name";
                p.Value = tableName;
                command.Parameters.Add(p);
                if (!string.IsNullOrEmpty(schema))
                {
                    var ps = command.CreateParameter();
                    ps.ParameterName = "@schema";
                    ps.Value = schema;
                    command.Parameters.Add(ps);
                }
                var result = Convert.ToInt64(command.ExecuteScalar());
                return result > 0;
            }
            else if (provider.Contains("MySql", StringComparison.OrdinalIgnoreCase) || provider.Contains("MariaDb", StringComparison.OrdinalIgnoreCase))
            {
                command.CommandText = "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = @name;";
                var p = command.CreateParameter();
                p.ParameterName = "@name";
                p.Value = tableName;
                command.Parameters.Add(p);
                var result = Convert.ToInt64(command.ExecuteScalar());
                return result > 0;
            }
            else
            {
                var creator = context.Database.GetService<Microsoft.EntityFrameworkCore.Storage.IDatabaseCreator>()
                    as Microsoft.EntityFrameworkCore.Storage.RelationalDatabaseCreator;
                return creator?.HasTables() ?? true;
            }
        }
        catch
        {
            return false;
        }
        finally
        {
            if (openedHere)
            {
                connection.Close();
            }
        }
    }

    private static void SeedIdentityData(IServiceProvider services, ILogger? logger)
    {
        try
        {
            var appUserType = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
                .FirstOrDefault(t => t.Name == "ApplicationUser");

            var appRoleType = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
                .FirstOrDefault(t => t.Name == "ApplicationRole");

            var profileRegistryType = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
                .FirstOrDefault(t => t.Name == "ProfileRegistry");

            var identitySettingsType = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
                .FirstOrDefault(t => t.Name == "IdentitySettings");

            var userServiceType = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
                .FirstOrDefault(t => t.Name == "IUserService");

            if (appUserType == null || appRoleType == null || profileRegistryType == null) return;

            var roleManagerType = typeof(Microsoft.AspNetCore.Identity.RoleManager<>).MakeGenericType(appRoleType);
            var roleManager = services.GetService(roleManagerType);
            var profileRegistry = services.GetService(profileRegistryType);
            var identitySettings = identitySettingsType != null ? services.GetService(identitySettingsType) : null;
            var userService = userServiceType != null ? services.GetService(userServiceType) : null;

            if (profileRegistry != null && roleManager != null)
            {
                var getAllDeclaredRolesMethod = profileRegistryType.GetMethod("GetAllDeclaredRoles");
                if (getAllDeclaredRolesMethod?.Invoke(profileRegistry, null) is IEnumerable<string> declaredRoles)
                {
                    var roleExistsMethod = roleManagerType.GetMethod("RoleExistsAsync");
                    var createRoleMethod = roleManagerType.GetMethod("CreateAsync");

                    foreach (var roleName in declaredRoles)
                    {
                        if (roleExistsMethod?.Invoke(roleManager, [roleName]) is Task<bool> existsTask)
                        {
                            if (!existsTask.GetAwaiter().GetResult())
                            {
                                var newRole = Activator.CreateInstance(appRoleType, roleName);
                                if (createRoleMethod?.Invoke(roleManager, [newRole]) is Task createTask)
                                {
                                    createTask.GetAwaiter().GetResult();
                                    logger?.LogInformation("Created role '{RoleName}'", roleName);
                                }
                            }
                        }
                    }
                }

                if (identitySettings != null && userService != null)
                {
                    var seedAdminEmail = identitySettingsType?.GetProperty("SeedAdminEmail")?.GetValue(identitySettings) as string;
                    var seedAdminPassword = identitySettingsType?.GetProperty("SeedAdminPassword")?.GetValue(identitySettings) as string;

                    if (!string.IsNullOrWhiteSpace(seedAdminEmail) && !string.IsNullOrWhiteSpace(seedAdminPassword))
                    {
                        var userManagerType = typeof(Microsoft.AspNetCore.Identity.UserManager<>).MakeGenericType(appUserType);
                        var userManager = services.GetService(userManagerType);
                        var findByEmailMethod = userManagerType.GetMethod("FindByEmailAsync");

                        if (findByEmailMethod?.Invoke(userManager, [seedAdminEmail]) is Task findTask)
                        {
                            dynamic taskResult = findTask;
                            if (taskResult.Result == null)
                            {
                                var createUserMethod = userServiceType?.GetMethod("CreateUserAsync");
                                if (createUserMethod != null)
                                {
                                    var createTask = createUserMethod.Invoke(userService, [seedAdminEmail, "admin", seedAdminPassword, "System", CancellationToken.None]);
                                    if (createTask is Task createAdminTask)
                                    {
                                        createAdminTask.GetAwaiter().GetResult();
                                        logger?.LogInformation("Seeded admin user '{Email}'", seedAdminEmail);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger?.LogDebug(ex, "Identity data seed skipped: {Message}", ex.Message);
        }
    }
}
