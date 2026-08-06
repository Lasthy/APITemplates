using Kernel.Modules;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace Kernel.Events;

public static class WebApplicationBuilderExtensions
{
    public static void AddEventBus(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<IEventBus, InProcessEventBus>();
    }

    public static void AddEventHandlersFromModule<T>(this WebApplicationBuilder builder)
        where T : IModule
    {
        // TODO: make this better later idk
        var types = typeof(T).Assembly
            .GetTypes()
            .Where(t => t.GetInterfaces()
                .Any(i => i.MakeGenericType() == typeof(IEventHandler<>)))
            .Select(t => ( type: t.GetInterfaces().First(i => i.MakeGenericType() == typeof(IEventHandler<>)), implementation: t));
        
        foreach((var type, var implementation) in types)
        {
            builder.Services.AddTransient(type, implementation);
        }
    }
}