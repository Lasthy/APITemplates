using Host.Extensions;
using Kernel.Extensions;
using Kernel.Modules;

var builder = WebApplication.CreateBuilder(args);

// Register all cross-cutting Kernel services (JSON, context, events, messaging, OpenAPI).
builder.AddKernel();

// Discover and register all active modules from settings.Instance.json.
builder.AddModules();

var app = builder.Build();

// Map the kernel OpenAPI JSON endpoint at /openapi/kernel.json.
app.UseKernel();

// Map module endpoints, wire up per-module OpenAPI documents + event types.
app.UseModules();

// Mount OpenAPI viewer (Scalar or Swagger) configured in appsettings.json
app.UseOpenApiViewer();

app.UseHttpsRedirection();

app.Run();