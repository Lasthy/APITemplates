using System.Net;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Kernel.Configuration;
using Kernel.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;

namespace Kernel.RateLimiting.Extensions;

/// <summary>
/// Extension methods for configuring and enabling rate limiting middleware.
/// </summary>
public static class RateLimitingExtensions
{
    /// <summary>
    /// Registers <see cref="RateLimitingSettings"/> and configures ASP.NET Core rate limiting with
    /// unauthenticated (per IP), authenticated (per User ID), and per-endpoint override policies.
    /// </summary>
    public static WebApplicationBuilder AddRateLimiting(this WebApplicationBuilder builder)
    {
        var dynamicSettings = builder.Services
            .Where(d => d.ServiceType == typeof(DynamicSettings))
            .Select(d => d.ImplementationInstance as DynamicSettings)
            .FirstOrDefault();

        var jsonSerializer = builder.Services
            .Where(d => d.ServiceType == typeof(IJsonSerializer))
            .Select(d => d.ImplementationInstance as IJsonSerializer)
            .FirstOrDefault() ?? new JsonSerializer();

        var environment = dynamicSettings?.Environment ?? builder.Environment.EnvironmentName;
        var rateLimitingSettings = new RateLimitingSettings(jsonSerializer, environment);

        if (dynamicSettings != null)
        {
            dynamicSettings.AddSection(rateLimitingSettings);
        }

        builder.Services.AddSingleton(rateLimitingSettings);

        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.OnRejected = async (context, token) =>
            {
                var settings = context.HttpContext.RequestServices.GetService<RateLimitingSettings>() ?? rateLimitingSettings;
                var statusCode = settings.RejectionStatusCode;
                var message = settings.RejectionMessage;

                context.HttpContext.Response.StatusCode = statusCode;
                context.HttpContext.Response.ContentType = "application/json";

                await context.HttpContext.Response.WriteAsJsonAsync(new
                {
                    statusCode = statusCode,
                    message = message
                }, cancellationToken: token);
            };

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
            {
                var settings = httpContext.RequestServices.GetService<RateLimitingSettings>() ?? rateLimitingSettings;

                if (!settings.Enabled)
                {
                    return RateLimitPartition.GetNoLimiter("disabled");
                }

                var path = httpContext.Request.Path.Value?.Trim('/') ?? string.Empty;

                // Check for endpoint override match (exact match, suffix match, or contained key)
                RateLimitEndpointConfig? matchedOverride = null;
                string? matchedOverrideKey = null;

                if (settings.EndpointOverrides.Count > 0)
                {
                    foreach (var (key, overrideConfig) in settings.EndpointOverrides)
                    {
                        var normKey = key.Trim('/');
                        if (path.Equals(normKey, StringComparison.OrdinalIgnoreCase) ||
                            path.EndsWith("/" + normKey, StringComparison.OrdinalIgnoreCase) ||
                            path.EndsWith(normKey, StringComparison.OrdinalIgnoreCase))
                        {
                            // Choose the longest / most specific match
                            if (matchedOverrideKey == null || normKey.Length > matchedOverrideKey.Length)
                            {
                                matchedOverrideKey = normKey;
                                matchedOverride = overrideConfig;
                            }
                        }
                    }
                }

                var isAuthenticated = httpContext.User?.Identity?.IsAuthenticated == true;
                var userId = isAuthenticated
                    ? (httpContext.User?.FindFirst("sub")?.Value ??
                       httpContext.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
                       httpContext.User?.Identity?.Name ??
                       "authenticated_user")
                    : null;

                string partitionKey;
                int permitLimit;
                int windowSeconds;
                int queueLimit;
                int segmentsPerWindow;

                if (isAuthenticated && !string.IsNullOrEmpty(userId))
                {
                    if (matchedOverride != null)
                    {
                        partitionKey = $"user:{userId}:endpoint:{matchedOverrideKey}";
                        permitLimit = matchedOverride.PermitLimit;
                        windowSeconds = matchedOverride.WindowSeconds;
                        queueLimit = matchedOverride.QueueLimit;
                        segmentsPerWindow = matchedOverride.SegmentsPerWindow;
                    }
                    else
                    {
                        partitionKey = $"user:{userId}";
                        var policy = settings.AuthenticatedPolicy;
                        permitLimit = policy.PermitLimit;
                        windowSeconds = policy.WindowSeconds;
                        queueLimit = policy.QueueLimit;
                        segmentsPerWindow = policy.SegmentsPerWindow;
                    }
                }
                else
                {
                    var clientIp = GetClientIpAddress(httpContext, settings);
                    if (matchedOverride != null)
                    {
                        partitionKey = $"ip:{clientIp}:endpoint:{matchedOverrideKey}";
                        permitLimit = matchedOverride.PermitLimit;
                        windowSeconds = matchedOverride.WindowSeconds;
                        queueLimit = matchedOverride.QueueLimit;
                        segmentsPerWindow = matchedOverride.SegmentsPerWindow;
                    }
                    else
                    {
                        partitionKey = $"ip:{clientIp}";
                        var policy = settings.GlobalPolicy;
                        permitLimit = policy.PermitLimit;
                        windowSeconds = policy.WindowSeconds;
                        queueLimit = policy.QueueLimit;
                        segmentsPerWindow = policy.SegmentsPerWindow;
                    }
                }

                if (segmentsPerWindow > 1)
                {
                    return RateLimitPartition.GetSlidingWindowLimiter(
                        partitionKey,
                        _ => new SlidingWindowRateLimiterOptions
                        {
                            PermitLimit = permitLimit > 0 ? permitLimit : 1,
                            Window = TimeSpan.FromSeconds(windowSeconds > 0 ? windowSeconds : 60),
                            SegmentsPerWindow = segmentsPerWindow,
                            QueueLimit = queueLimit,
                            QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                        });
                }

                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey,
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = permitLimit > 0 ? permitLimit : 1,
                        Window = TimeSpan.FromSeconds(windowSeconds > 0 ? windowSeconds : 60),
                        QueueLimit = queueLimit,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                    });
            });
        });

        return builder;
    }

    /// <summary>
    /// Enables rate limiting middleware in the HTTP request pipeline.
    /// </summary>
    public static WebApplication UseRateLimiting(this WebApplication app)
    {
        app.UseRateLimiter();
        return app;
    }

    private static string GetClientIpAddress(HttpContext context, RateLimitingSettings settings)
    {
        if (settings.TrustForwardedHeaders)
        {
            var forwardedHeader = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(forwardedHeader))
            {
                var firstIp = forwardedHeader.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(firstIp))
                {
                    return firstIp;
                }
            }
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown_client_ip";
    }
}
