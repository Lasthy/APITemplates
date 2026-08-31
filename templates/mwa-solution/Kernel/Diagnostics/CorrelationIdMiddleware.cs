using Microsoft.AspNetCore.Http;

namespace Kernel.Diagnostics;

/// <summary>
/// Middleware that inspects incoming requests for an <c>X-Correlation-Id</c> header.
/// If not present, generates a new <see cref="DefaultId"/> (Ulid).
/// Stores the correlation ID in <see cref="HttpContext.Items"/>, adds it to the HTTP response headers,
/// and attaches it as a tag to the current diagnostic activity.
/// </summary>
public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[DiagnosticsConstants.CorrelationIdHeader].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            correlationId = DefaultId.NewUlid().ToString();
        }

        context.Items[DiagnosticsConstants.CorrelationIdItemKey] = correlationId;

        if (!context.Response.Headers.ContainsKey(DiagnosticsConstants.CorrelationIdHeader))
        {
            context.Response.Headers[DiagnosticsConstants.CorrelationIdHeader] = correlationId;
        }

        using var activity = DiagnosticsConstants.ActivitySource.StartActivity("HttpRequest");
        activity?.SetTag(DiagnosticsConstants.CorrelationIdTag, correlationId);

        await _next(context);
    }
}
