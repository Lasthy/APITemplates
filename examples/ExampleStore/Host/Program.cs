using Host.Extensions;
using Kernel.Extensions;
using Kernel.Modules;

var builder = WebApplication.CreateBuilder(args);

// Register all cross-cutting Kernel services (JSON, context, events, messaging, OpenAPI).
builder.AddKernel();

// Register database options and persistence contexts from Data layer.
builder.AddData();

// Discover and register all active modules from appsettings.Instance.json.
builder.AddModules();

var app = builder.Build();

// Map the kernel OpenAPI JSON endpoint at /openapi/kernel.json.
app.UseKernel();

// Authentication and Authorization middleware
app.UseAuthentication();
app.UseAuthorization();

// Ensure databases and tables are created automatically on startup
app.UseData();

// Map module endpoints, wire up per-module OpenAPI documents + event types.
app.UseModules();

// Mount OpenAPI viewer (Scalar or Swagger) configured in appsettings.Instance.json
app.UseOpenApiViewer();

app.UseHttpsRedirection();

app.Run();

public partial class Program { }