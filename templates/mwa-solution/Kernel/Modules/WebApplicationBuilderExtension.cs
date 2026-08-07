using Kernel.Configuration;
using Kernel.Events;
using Kernel.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Kernel.Modules;

public static class WebApplicationBuilderExtension
{
    public class CommonCollections
    {
        public EventTypeRegistry EventRegistry = new();
    }

    public static void AddModules(this WebApplicationBuilder builder)
    {
        var jsonSerializer = new JsonSerializer();
        var dynamicSettings = new DynamicSettings(jsonSerializer);

        var instanceSettings = dynamicSettings.AddSection(new InstanceSettings("appsettings.Instance.json", jsonSerializer));

        var modules = instanceSettings.ActiveModules;
        var addModuleMethod = typeof(WebApplicationBuilderExtension).GetMethod(nameof(AddModule), BindingFlags.Public | BindingFlags.Static)!;

        foreach ( (var module, var isActive) in modules )
        {
            if (!isActive) continue;

            var assembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(asm => string.Equals(asm.GetName().Name, module, StringComparison.InvariantCultureIgnoreCase));

            if(assembly == null)
            {
                throw new ApplicationException($"Module {module} assembly wasn't found");
            }

            var moduleType = assembly.GetTypes()
                .FirstOrDefault(t => t.GetInterface(nameof(IModule)) != null);

            if(moduleType == null)
            {
                throw new ApplicationException($"Module {module} doesn't have a class that inherits IModule.");
            }

            var method = addModuleMethod.MakeGenericMethod(moduleType);

            method.Invoke(null, []);
        }
    }

    public static void AddModule<T>(this WebApplicationBuilder builder) where T : IModule
    {
        T.Register(builder.Services, builder.Configuration);

        RegisterEventHandlers(builder.Services, typeof(T).Assembly);
    }

    private static void RegisterEventHandlers(IServiceCollection services, Assembly assembly)
    {
        var handlerInterface = typeof(IEventHandler<>);
        var implementations = assembly.GetTypes()
            .Where(t => !t.IsAbstract && !t.IsInterface &&
                       t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == handlerInterface))
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
