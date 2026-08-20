using Kernel.Extensions;
using Scalar.AspNetCore;

namespace Host.Extensions;

public static class OpenApiViewerExtensions
{
    public static void UseOpenApiViewer(this WebApplication app)
    {
        // ── OpenAPI viewer UI ──────────────────────────────────────────────────────
        // Reads Api.OpenApiViewer from appsettings.json: "scalar" (default) or "swagger".
        // Only served in Development — remove the environment check to expose in other environments.
        if (app.Environment.IsDevelopment())
        {
            var apiSettings = app.GetApiSettings();
            var moduleRegistry = app.Services.GetService(typeof(Kernel.Modules.ModuleRegistry)) as Kernel.Modules.ModuleRegistry;

            if (apiSettings.OpenApiViewer.Equals("swagger", StringComparison.OrdinalIgnoreCase))
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

                    // Kernel document
                    options.AddDocument("Kernel", "/openapi/kernel.json");

                    // Per-module documents
                    if (moduleRegistry != null)
                    {
                        foreach (var module in moduleRegistry.Modules)
                        {
                            options.AddDocument(module.Name, $"/openapi/{module.Name}.json");
                        }
                    }
                });
            }
        }
    }
}
