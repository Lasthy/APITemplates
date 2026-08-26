using Kernel.Configuration;
using Kernel.Extensions;
using Kernel.Modules;
using Scalar.AspNetCore;

namespace Host.Extensions;

public static class OpenApiViewerExtensions
{
    public static void UseOpenApiViewer(this WebApplication app)
    {
        // ── OpenAPI viewer UI ──────────────────────────────────────────────────────
        // Reads OpenApiViewer from appsettings.Instance.json: "scalar" (default) or "swagger".
        // Only served in Development — remove the environment check to expose in other environments.
        if (app.Environment.IsDevelopment())
        {
            var instanceSettings = app.Services.GetRequiredService<InstanceSettings>();
            var moduleRegistry = app.Services.GetService(typeof(ModuleRegistry)) as ModuleRegistry;

            if (instanceSettings.OpenApiViewer.Equals("swagger", StringComparison.OrdinalIgnoreCase))
            {
                // Swagger UI — served at /docs
                app.UseSwaggerUI(options =>
                {
                    options.SwaggerEndpoint("/openapi/kernel.json", "Kernel");

                    if (moduleRegistry != null)
                    {
                        foreach (var module in moduleRegistry.Modules)
                        {
                            options.SwaggerEndpoint($"/openapi/{module.Name}.json", module.Name);
                        }
                    }

                    options.RoutePrefix = "docs";
                });
            }
            else
            {
                // Scalar UI (default) — served at /scalar
                app.MapScalarApiReference(options =>
                {
                    options.Title = "API Reference";

                    var documentNames = new List<string> { "kernel" };

                    if (moduleRegistry != null)
                    {
                        foreach (var module in moduleRegistry.Modules)
                        {
                            documentNames.Add(module.Name);
                        }
                    }

                    options.AddDocuments(documentNames);
                });
            }
        }
    }
}
