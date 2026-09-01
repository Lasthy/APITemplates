using System.Reflection;
using Kernel.Authorization.Extensions;
using Kernel.Configuration;
using Kernel.Events;
using Kernel.Health;
using Kernel.Json;
using Kernel.Modules;
using Kernel.Requests;
using Kernel.Validation.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kernel.Testing.Infrastructure;

/// <summary>
/// Base test fixture for testing a specific module <typeparamref name="TModule"/>.
/// Automatically registers module services, handlers, event handlers, validators, and policies into an isolated in-memory container.
/// </summary>
/// <typeparam name="TModule">The module type implementing <see cref="IModule"/>.</typeparam>
public abstract class ModuleTestBase<TModule> : KernelTestBase where TModule : IModule
{
    protected override void ConfigureServices(IServiceCollection services)
    {
        base.ConfigureServices(services);

        var assembly = typeof(TModule).Assembly;
        var jsonSerializer = new JsonSerializer();

        // Auto-discover DynamicSettingsSection subclasses in the module assembly
        RegisterModuleSettingsSections(DynamicSettings, assembly, jsonSerializer);

        // Register module services
        TModule.Register(services, Configuration);

        // Register event handlers from module assembly
        RegisterEventHandlers(services, assembly);

        // Register request handlers from module assembly
        RegisterRequestHandlers(services, assembly);

        // Register validators from module assembly
        services.RegisterValidatorsFromAssembly(assembly);

        // Register authorization policies from module assembly
        services.RegisterAuthorizationPoliciesFromAssembly(assembly);

        // Register health checks
        RegisterHealthChecks(services, assembly);
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();

        // Register event types for deserialization
        var eventTypeRegistry = Services.GetRequiredService<IEventTypeRegistry>();
        eventTypeRegistry.RegisterFromAssembly(typeof(TModule).Assembly);

        // Seed declared profile roles if Identity is present
        await SeedDeclaredRolesAsync();
    }

    private async Task SeedDeclaredRolesAsync()
    {
        using var scope = CreateScope();
        var appRoleType = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
            .FirstOrDefault(t => t.Name == "ApplicationRole");

        var profileRegistryType = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
            .FirstOrDefault(t => t.Name == "ProfileRegistry");

        if (appRoleType == null || profileRegistryType == null) return;

        var roleManagerType = typeof(Microsoft.AspNetCore.Identity.RoleManager<>).MakeGenericType(appRoleType);
        var roleManager = scope.ServiceProvider.GetService(roleManagerType);
        var profileRegistry = scope.ServiceProvider.GetService(profileRegistryType);

        if (roleManager != null && profileRegistry != null)
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
                        if (!await existsTask)
                        {
                            var newRole = Activator.CreateInstance(appRoleType, roleName);
                            if (createRoleMethod?.Invoke(roleManager, [newRole]) is Task createTask)
                            {
                                await createTask;
                            }
                        }
                    }
                }
            }
        }
    }

    private void RegisterModuleSettingsSections(
        DynamicSettings dynamicSettings,
        Assembly assembly,
        IJsonSerializer jsonSerializer)
    {
        var sectionTypes = assembly.GetTypes()
            .Where(t => !t.IsAbstract && t.IsSubclassOf(typeof(DynamicSettingsSection)))
            .ToList();

        foreach (var sectionType in sectionTypes)
        {
            try
            {
                var instance = Activator.CreateInstance(sectionType, jsonSerializer, "Testing") as DynamicSettingsSection
                    ?? Activator.CreateInstance(sectionType) as DynamicSettingsSection;

                if (instance != null)
                {
                    instance.AutoSave = false;
                    if (!dynamicSettings.GetSectionNames().Any(n => string.Equals(n, instance.SectionName, StringComparison.OrdinalIgnoreCase)))
                    {
                        dynamicSettings.AddSection(instance);
                    }
                }
            }
            catch
            {
                // Fallback if parameterless or standard constructor is not available
            }
        }
    }

    private static void RegisterEventHandlers(IServiceCollection services, Assembly assembly)
    {
        var handlerInterface = typeof(IEventHandler<>);
        var implementations = assembly.GetTypes()
            .Where(t => !t.IsAbstract && !t.IsInterface && t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == handlerInterface))
            .ToList();

        foreach (var implementation in implementations)
        {
            var interfaces = implementation.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == handlerInterface);

            foreach (var @interface in interfaces)
            {
                services.AddScoped(@interface, implementation);
            }
        }
    }

    private static void RegisterRequestHandlers(IServiceCollection services, Assembly assembly)
    {
        var handlerInterface = typeof(IRequestHandler<,>);
        var implementations = assembly.GetTypes()
            .Where(t => !t.IsAbstract && !t.IsInterface && t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == handlerInterface))
            .ToList();

        foreach (var implementation in implementations)
        {
            var interfaces = implementation.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == handlerInterface);

            foreach (var @interface in interfaces)
            {
                services.AddScoped(@interface, implementation);
            }
        }
    }

    private void RegisterHealthChecks(IServiceCollection services, Assembly assembly)
    {
        var checkTypes = assembly.GetTypes()
            .Where(t => !t.IsAbstract && !t.IsInterface && typeof(IHealthCheck).IsAssignableFrom(t))
            .ToList();

        var healthCheckRegistry = services
            .Where(d => d.ServiceType == typeof(IHealthCheckRegistry))
            .Select(d => d.ImplementationInstance as IHealthCheckRegistry)
            .FirstOrDefault();

        foreach (var checkType in checkTypes)
        {
            services.AddScoped(checkType);
            healthCheckRegistry?.Register(TModule.Name, checkType);
        }
    }
}

/// <summary>
/// Base test fixture for testing a specific module <typeparamref name="TModule"/> with a direct reference
/// to its primary EF Core <typeparamref name="TDbContext"/>.
/// </summary>
/// <typeparam name="TModule">The module type implementing <see cref="IModule"/>.</typeparam>
/// <typeparam name="TDbContext">The module's EF Core DbContext.</typeparam>
public abstract class ModuleTestBase<TModule, TDbContext> : ModuleTestBase<TModule>
    where TModule : IModule
    where TDbContext : DbContext
{
    /// <summary>
    /// The primary DbContext for the module under test.
    /// </summary>
    public TDbContext DbContext => Services.GetRequiredService<TDbContext>();

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();

        // Ensure module DbContext tables are created in the in-memory database
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TDbContext>();
        await db.Database.EnsureCreatedAsync();
    }
}
