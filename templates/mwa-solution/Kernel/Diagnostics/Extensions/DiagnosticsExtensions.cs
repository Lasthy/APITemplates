using Kernel.Diagnostics;
using Microsoft.AspNetCore.Builder;

namespace Kernel.Diagnostics.Extensions;

/// <summary>
/// Extension methods for configuring observability and diagnostics in ASP.NET Core pipelines.
/// </summary>
public static class DiagnosticsExtensions
{
    /// <summary>
    /// Adds <see cref="CorrelationIdMiddleware"/> to the pipeline to ensure every HTTP request has a correlation ID.
    /// </summary>
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app)
    {
        return app.UseMiddleware<CorrelationIdMiddleware>();
    }

    /// <summary>
    /// Adds <see cref="RequestDiagnosticsMiddleware"/> to the pipeline to log structured request/response metrics.
    /// </summary>
    public static IApplicationBuilder UseRequestDiagnostics(this IApplicationBuilder app)
    {
        return app.UseMiddleware<RequestDiagnosticsMiddleware>();
    }
}
