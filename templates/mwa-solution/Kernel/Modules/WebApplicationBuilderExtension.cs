using Kernel.Configuration;
using Kernel.Events;
using Kernel.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Kernel.Modules;

/// <summary>
/// Extension methods for registering and configuring modules on the
/// <see cref="WebApplicationBuilder"/> and <see cref="WebApplication"/>.
/// </summary>
public static class WebApplicationBuilderExtension
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
        var dynamicSettings = builder.Services
            .Where(d => d.ServiceType == typeof(DynamicSettings))
            .Select(d => d.ImplementationInstance as DynamicSettings)
            .FirstOrDefault();

        if (dynamicSettings == null)
        {
            throw new ApplicationException("DynamicSettings not found in DI. Did you call AddKernel() before AddModules()?");
        }

        var jsonSerializer = new JsonSerializer();
        InstanceSettings instanceSettings;
        try { instanceSettings = dynamicSettings.GetSection<InstanceSettings>(); }
        catch { instanceSettings = dynamicSettings.AddSection(new InstanceSettings("appsettings.Instance.json", jsonSerializer)); }

        var modules = instanceSettings.ActiveModules;
        var addModuleMethod = typeof(WebApplicationBuilderExtension)
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
    }

    /// <summary>
    /// Registers a single module of type <typeparamref name="T"/>:
    /// <list type="bullet">
    ///   <item>Calls <see cref="IModule.Register"/> to register module services.</item>
    ///   <item>Registers a named OpenAPI document for the module.</item>
    ///   <item>Scans and registers all <see cref="IEventHandler{TEvent}"/> implementations.</item>
    ///   <item>Records the module in the <see cref="ModuleRegistry"/> for post-build use.</item>
    /// </list>
    /// </summary>
    public static void AddModule<T>(this WebApplicationBuilder builder) where T : IModule
    {
        // Load module specific settings
        var dynamicSettings = builder.Services
            .Where(d => d.ServiceType == typeof(DynamicSettings))
            .Select(d => d.ImplementationInstance as DynamicSettings)
            .FirstOrDefault();
            
        if (dynamicSettings != null)
        {
            var settingsPath = $"appsettings.{T.Name}.json";
            if (File.Exists(settingsPath))
            {
                var jsonSerializer = new JsonSerializer();
                var moduleSettings = new DynamicSettingsSection(settingsPath, jsonSerializer);
                dynamicSettings.CreateSection(settingsPath, T.Name);
            }
        }

        // Register module authorization policies
        builder.Services.AddAuthorization(options => T.RegisterPolicies(options));

        // Register module services.
        T.Register(builder.Services, builder.Configuration);

        // Register a named OpenAPI document for this module.
        builder.Services.AddOpenApi(T.Name);

        // Register all IEventHandler<> implementations from the module's assembly.
        RegisterEventHandlers(builder.Services, typeof(T).Assembly);

        // Record in the ModuleRegistry for post-build MapEndpoints and event type wiring.
        var registry = builder.Services
            .Where(d => d.ServiceType == typeof(ModuleRegistry))
            .Select(d => d.ImplementationInstance as ModuleRegistry)
            .FirstOrDefault();

        if (registry == null)
        {
            throw new ApplicationException("ModuleRegistry not found in DI. Did you call AddKernel() before AddModules()?");
        }

        registry.Register(new ModuleDescriptor(
            Name: T.Name,
            Assembly: typeof(T).Assembly,
            MapEndpoints: T.MapEndpoints));
    }

    /// <summary>
    /// Configures the HTTP pipeline for all registered modules:
    /// <list type="bullet">
    ///   <item>Registers event types from each module's assembly into <see cref="IEventTypeRegistry"/>.</item>
    ///   <item>Maps each module's <see cref="IModule.MapEndpoints"/> routes.</item>
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
        }
    }

    // -----------------------------------------------------------------------

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

}
