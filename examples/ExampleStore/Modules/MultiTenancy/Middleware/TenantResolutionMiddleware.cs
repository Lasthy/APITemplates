using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using MultiTenancy.Configuration;
using MultiTenancy.Contracts.Constants;
using MultiTenancy.Persistence;
using MultiTenancy.Resolution;
using MultiTenancy.Services;

namespace MultiTenancy.Middleware;

/// <summary>
/// Pipeline middleware that resolves the current tenant per request via the configured <see cref="ITenantResolver"/>,
/// validates tenant lifecycle status (active, suspended, deleted), enforces the <c>RequireTenant</c> policy,
/// and sets the active tenant on <see cref="ITenantContext"/>.
/// </summary>
public class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;

    public TenantResolutionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        ITenantResolver resolver,
        ITenantContext tenantContext,
        TenantSettings settings,
        TenantModuleDbContext dbContext)
    {
        var tenantId = await resolver.ResolveAsync(context, context.RequestAborted);

        if (tenantId.HasValue && tenantId.Value != default)
        {
            var tenant = await dbContext.Tenants
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == tenantId.Value, context.RequestAborted);

            if (tenant == null || tenant.Status == TenantStatus.Deleted)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                await context.Response.WriteAsJsonAsync(new
                {
                    title = "Not Found",
                    detail = $"Tenant '{tenantId.Value}' not found."
                });
                return;
            }

            if (tenant.Status == TenantStatus.Suspended)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new
                {
                    title = "Forbidden",
                    detail = "Tenant is suspended.",
                    reason = tenant.SuspensionReason
                });
                return;
            }

            tenantContext.SetTenantId(tenantId.Value);
        }
        else
        {
            var endpoint = context.GetEndpoint();
            var isOptional = endpoint?.Metadata.GetMetadata<TenantOptionalAttribute>() != null;

            if (settings.RequireTenant && !isOptional)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsJsonAsync(new
                {
                    title = "Bad Request",
                    detail = "Tenant resolution is required for this endpoint."
                });
                return;
            }
        }

        await _next(context);
    }
}
