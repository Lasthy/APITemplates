using Kernel.Context;
using Kernel.Requests;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Text.Json;
using Kernel.Configuration.Requests;
using Microsoft.AspNetCore.Mvc;

namespace Kernel.Configuration.Endpoints;

public static class SettingsEndpoints
{
    public static void MapSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/kernel/settings")
                       .WithTags("Settings")
                       .WithGroupName("kernel")
                       .RequireAuthorization(p => p.RequireRole(Roles.System));

        group.MapGet("/", (DynamicSettings settings) =>
        {
            return Results.Ok(settings.GetSectionNames());
        });

        group.MapGet("/{sectionName}", async (string sectionName, IDispatcher dispatcher, CancellationToken ct) =>
        {
            var result = await dispatcher.DispatchAsync(new GetSettingsRequest(sectionName), ct);
            return result.Match(
                success => Results.Content(success, "application/json"),
                failure => Results.BadRequest(failure.Message)
            );
        });

        group.MapPut("/{sectionName}/{key}", async (string sectionName, string key, [FromBody] JsonElement value, IDispatcher dispatcher, CancellationToken ct) =>
        {
            var result = await dispatcher.DispatchAsync(new UpdateSettingsRequest(sectionName, key, value), ct);
            return result.Match(
                success => Results.NoContent(),
                failure => Results.BadRequest(failure.Message)
            );
        });
    }
}
