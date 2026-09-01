using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Kernel.Diagnostics;

/// <summary>
/// Middleware that logs HTTP request execution diagnostics including method, path,
/// HTTP status code, duration in milliseconds, correlation ID, and authenticated user ID.
/// </summary>
public class RequestDiagnosticsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestDiagnosticsMiddleware> _logger;

    public RequestDiagnosticsMiddleware(RequestDelegate next, ILogger<RequestDiagnosticsMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            await _next(context);
        }
        finally
        {
            sw.Stop();

            var correlationId = context.Items[DiagnosticsConstants.CorrelationIdItemKey] as string
                ?? context.Request.Headers[DiagnosticsConstants.CorrelationIdHeader].FirstOrDefault();

            var userId = context.User?.FindFirst("sub")?.Value
                ?? context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            _logger.LogInformation(
                "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs}ms [CorrelationId={CorrelationId}] [User={UserId}]",
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                sw.ElapsedMilliseconds,
                correlationId,
                userId);
        }
    }
}
