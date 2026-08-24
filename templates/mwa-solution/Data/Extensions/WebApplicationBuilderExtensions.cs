using Data.Configuration;
using Data.Health;
using Data.Persistence;
using Kernel.Configuration;
using Kernel.Health;
using Kernel.Json;
using Kernel.Persistence;
using Kernel.Persistence.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Data.Extensions;

public static class WebApplicationBuilderExtensions
{
    /// <summary>
    /// Registers the <see cref="DataSettings"/> dynamic section and the <see cref="KernelDbContext"/>
    /// (bound to <see cref="IKernelDbContext"/>) configured with the database provider specified in <c>settings.Data.json</c>.
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
            options.UseConfiguredDatabase(dataSettings.Provider, dataSettings.ConnectionString);
            options.AddKernelInterceptors(sp);
        });

        builder.Services.AddScoped<IKernelDbContext>(sp => sp.GetRequiredService<KernelDbContext>());

        // Register Database Health Check
        builder.Services.AddScoped<DatabaseHealthCheck>();
        var healthCheckRegistry = builder.Services
            .Where(d => d.ServiceType == typeof(IHealthCheckRegistry))
            .Select(d => d.ImplementationInstance as IHealthCheckRegistry)
            .FirstOrDefault();

        healthCheckRegistry?.Register<DatabaseHealthCheck>("Data");
    }
}
