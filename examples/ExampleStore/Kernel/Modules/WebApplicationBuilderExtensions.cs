using Kernel.Authorization.Extensions;
using Kernel.Configuration;
using Kernel.Events;
using Kernel.Extensions;
using Kernel.Health;
using Kernel.Json;
using Kernel.Requests;
using Kernel.Validation.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Kernel.Modules;

/// <summary>
/// Extension methods for registering and configuring modules on the
/// <see cref="WebApplicationBuilder"/> and <see cref="WebApplication"/>.
/// </summary>
public static class WebApplicationBuilderExtensions
{
    /// <summary>
    /// Discovers and registers all active modules from <c>appsettings.Instance.json</c>.
    /// For each active module it calls <see cref="AddModule{T}"/>, which registers module
    /// services, per-module OpenAPI document, and event handlers.
    /// </summary>
    /// <exception cref="ApplicationException">
    /// Thrown when an active module's assembly or <see cref="IModule"/> implementation is not found.
    /// </exception>
    public static void AddModules(this WebApplicationBuilder builder)
    {
        var dynamicSettings = builder.Services.GetRegisteredSingleton<DynamicSettings>();
        if (dynamicSettings == null)
        {
            throw new ApplicationException("DynamicSettings not found in DI. Did you call AddKernel() before AddModules()?");
        }

        InstanceSettings instanceSettings;
        try { instanceSettings = dynamicSettings.GetSection<InstanceSettings>(); }
        catch
        {
            var jsonSerializer = new JsonSerializer();
            instanceSettings = dynamicSettings.AddSection(
                new InstanceSettings(jsonSerializer, dynamicSettings.Environment));
        }

        var modules = instanceSettings.ActiveModules;
        var addModuleMethod = typeof(WebApplicationBuilderExtensions)
            .GetMethod(nameof(AddModule), BindingFlags.Public | BindingFlags.Static)!;

        foreach (var (moduleName, isActive) in modules)
        {
            if (!isActive) continue;

            var assembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(asm => string.Equals(
                    asm.GetName().Name, moduleName, StringComparison.OrdinalIgnoreCase));

            if (assembly == null)
            {
                throw new ApplicationException(
                    $"Module '{moduleName}' assembly was not found in the current AppDomain. " +
                    $"Ensure the module project is referenced and its assembly is loaded.");
            }

            var moduleType = assembly.GetTypes()
                .FirstOrDefault(t =>
                    !t.IsAbstract &&
                    !t.IsInterface &&
                    t.GetInterfaces().Any(i => i.FullName == typeof(IModule).FullName));

            if (moduleType == null)
            {
                throw new ApplicationException(
                    $"Module '{moduleName}' does not contain a class implementing IModule.");
            }

            var method = addModuleMethod.MakeGenericMethod(moduleType);
            method.Invoke(null, [builder]);
        }

        // Register all DbContext types found in service descriptors into DbContextTypeRegistry if present
        var dbContextRegistry = builder.Services.GetRegisteredSingleton<Kernel.Persistence.DbContextTypeRegistry>();
        if (dbContextRegistry != null)
        {
            var dbContextTypes = builder.Services
                .Where(sd => typeof(Microsoft.EntityFrameworkCore.DbContext).IsAssignableFrom(sd.ServiceType) &&
                             sd.ServiceType != typeof(Microsoft.EntityFrameworkCore.DbContext))
                .Select(sd => sd.ServiceType)
                .Distinct();

            foreach (var type in dbContextTypes)
            {
                dbContextRegistry.Register(type);
            }
        }
    }

    /// <summary>
    /// Registers a single module of type <typeparamref name="T"/>:
    /// <list type="bullet">
    ///   <item>Loads module settings from <c>appsettings.{ModuleName}.json</c>.</item>
    ///   <item>Auto-discovers <see cref="DynamicSettingsSection"/> subclasses in the module assembly.</item>
    ///   <item>Calls <see cref="IModule.Register"/> to register module services.</item>
    ///   <item>Registers a named OpenAPI document for the module.</item>
    ///   <item>Scans and registers all <see cref="IEventHandler{TEvent}"/> implementations.</item>
    ///   <item>Records the module in the <see cref="ModuleRegistry"/> for post-build use.</item>
    /// </list>
    /// </summary>
    public static void AddModule<T>(this WebApplicationBuilder builder) where T : IModule
    {
        // Load module-specific settings
        var dynamicSettings = builder.Services.GetRegisteredSingleton<DynamicSettings>();
        if (dynamicSettings != null)
        {
            var jsonSerializer = new JsonSerializer();
            var environment = dynamicSettings.Environment;

            // Auto-discover DynamicSettingsSection subclasses in the module assembly
            RegisterModuleSettingsSections(dynamicSettings, typeof(T).Assembly, jsonSerializer, environment);

            // Fall back: if no custom section was found, create a generic one for the module
            var settingsPath = $"appsettings.{T.Name}.json";
            var resolvedSettingsPath = DynamicSettingsSection.ResolveFilePath(settingsPath, T.Name);
            if (!dynamicSettings.GetSectionNames().Any(n =>
                    string.Equals(n, T.Name, StringComparison.OrdinalIgnoreCase)) &&
                File.Exists(resolvedSettingsPath))
            {
                var moduleSettings = new GenericModuleSettings(T.Name, resolvedSettingsPath, jsonSerializer, environment);
                dynamicSettings.AddSection(moduleSettings);
            }
        }

        // Register module authorization policies
        builder.Services.AddAuthorization(options => T.RegisterPolicies(options));

        // Discover and register all IAuthorizationPolicy implementations from the module's assembly.
        builder.Services.RegisterAuthorizationPoliciesFromAssembly(typeof(T).Assembly);

        // Register module services.
        T.Register(builder.Services, builder.Configuration);

        // Register a named OpenAPI document for this module.
        builder.Services.AddOpenApi(T.Name, options =>
        {
            options.ShouldInclude = description =>
                string.Equals(description.GroupName, T.Name, StringComparison.OrdinalIgnoreCase);
        });

        // Register all IEventHandler<> implementations from the module's assembly.
        RegisterEventHandlers(builder.Services, typeof(T).Assembly);

        // Register all IRequestHandler<,> implementations from the module's assembly.
        RegisterRequestHandlers(builder.Services, typeof(T).Assembly);

        // Discover and register all IValidator<> implementations from the module's assembly.
        builder.Services.RegisterValidatorsFromAssembly(typeof(T).Assembly);

        // Discover and register all IHealthCheck implementations from the module's assembly.
        var healthCheckRegistry = builder.Services.GetRegisteredSingleton<IHealthCheckRegistry>();
        if (healthCheckRegistry != null)
        {
            RegisterHealthChecks(builder.Services, healthCheckRegistry, T.Name, typeof(T).Assembly);
        }

        // Record in the ModuleRegistry for post-build MapEndpoints, event type wiring, and startup hooks.
        var registry = builder.Services.GetRegisteredSingleton<ModuleRegistry>();
        if (registry == null)
        {
            throw new ApplicationException("ModuleRegistry not found in DI. Did you call AddKernel() before AddModules()?");
        }

        registry.Register(new ModuleDescriptor(
            Name: T.Name,
            Assembly: typeof(T).Assembly,
            MapEndpoints: T.MapEndpoints,
            OnStartup: T.OnStartup));
    }

    /// <summary>
    /// Configures the HTTP pipeline for all registered modules:
    /// <list type="bullet">
    ///   <item>Registers event types from each module's assembly into <see cref="IEventTypeRegistry"/>.</item>
    ///   <item>Maps each module's <see cref="IModule.MapEndpoints"/> routes.</item>
    ///   <item>Executes any module <see cref="IModule.OnStartup"/> hooks.</item>
    ///   <item>Exposes each module's OpenAPI document at <c>/openapi/{moduleName}.json</c>.</item>
    /// </list>
    /// Call this after <c>app.UseKernel()</c> in <c>Program.cs</c>.
    /// </summary>
    public static void UseModules(this WebApplication app)
    {
        var registry = app.Services.GetRequiredService<ModuleRegistry>();
        var eventTypeRegistry = app.Services.GetRequiredService<IEventTypeRegistry>();

        foreach (var module in registry.Modules)
        {
            // Register all IEvent implementations from this module for inbox/outbox deserialization.
            eventTypeRegistry.RegisterFromAssembly(module.Assembly);

            // Map module endpoints.
            module.MapEndpoints(app);

            // Execute module startup hook if defined
            module.OnStartup?.Invoke(app.Services);
        }
    }

    // -----------------------------------------------------------------------

    /// <summary>
    /// Scans a module assembly for concrete <see cref="DynamicSettingsSection"/> subclasses
    /// and registers them into <see cref="DynamicSettings"/>.
    /// </summary>
    private static void RegisterModuleSettingsSections(
        DynamicSettings dynamicSettings,
        Assembly assembly,
        IJsonSerializer jsonSerializer,
        string? environment)
    {
        var sectionTypes = assembly.GetTypes()
            .Where(t =>
                !t.IsAbstract &&
                t.IsSubclassOf(typeof(DynamicSettingsSection)))
            .ToList();

        foreach (var sectionType in sectionTypes)
        {
            // Try to construct with (IJsonSerializer, string?) signature
            var instance = Activator.CreateInstance(sectionType, jsonSerializer, environment)
                as DynamicSettingsSection;

            if (instance != null)
            {
                dynamicSettings.AddSection(instance);
            }
        }
    }

    private static void RegisterEventHandlers(IServiceCollection services, Assembly assembly)
    {
        var handlerInterface = typeof(IEventHandler<>);
        var implementations = assembly.GetTypes()
            .Where(t =>
                !t.IsAbstract &&
                !t.IsInterface &&
                t.GetInterfaces().Any(i =>
                    i.IsGenericType &&
                    i.GetGenericTypeDefinition() == handlerInterface))
            .ToList();

        foreach (var implementation in implementations)
        {
            var interfaces = implementation.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == handlerInterface)
                .ToList();

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
            .Where(t =>
                !t.IsAbstract &&
                !t.IsInterface &&
                t.GetInterfaces().Any(i =>
                    i.IsGenericType &&
                    i.GetGenericTypeDefinition() == handlerInterface))
            .ToList();

        foreach (var implementation in implementations)
        {
            var interfaces = implementation.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == handlerInterface)
                .ToList();

            foreach (var @interface in interfaces)
            {
                services.AddScoped(@interface, implementation);
            }
        }
    }

    private static void RegisterHealthChecks(
        IServiceCollection services,
        IHealthCheckRegistry healthCheckRegistry,
        string moduleName,
        Assembly assembly)
    {
        var checkTypes = assembly.GetTypes()
            .Where(t =>
                !t.IsAbstract &&
                !t.IsInterface &&
                typeof(IHealthCheck).IsAssignableFrom(t))
            .ToList();

        foreach (var checkType in checkTypes)
        {
            services.AddScoped(checkType);
            healthCheckRegistry.Register(moduleName, checkType);
        }
    }
}
