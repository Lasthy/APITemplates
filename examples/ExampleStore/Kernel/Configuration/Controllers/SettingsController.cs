using Asp.Versioning;
using Kernel.Common;
using Kernel.Configuration.Features;
using Kernel.Constants;
using Kernel.Context;
using Kernel.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Kernel.Configuration.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/kernel/settings")]
[Authorize(Roles = Roles.System)]
[ApiExplorerSettings(GroupName = "kernel")]
[Tags("Settings")]
public class SettingsController : ControllerBase
{
    private readonly DynamicSettings _dynamicSettings;
    private readonly IDispatcher _dispatcher;

    public SettingsController(DynamicSettings dynamicSettings, IDispatcher dispatcher)
    {
        _dynamicSettings = dynamicSettings;
        _dispatcher = dispatcher;
    }

    [HttpGet]
    public IActionResult GetSectionNames()
    {
        return Ok(_dynamicSettings.GetSectionNames());
    }

    [HttpGet("{sectionName}")]
    public async Task<IActionResult> GetSection([FromRoute] string sectionName, CancellationToken ct)
    {
        var result = await _dispatcher.DispatchAsync(new GetSettings(sectionName), ct);
        return result.Match<IActionResult>(
            success => Content(success, "application/json"),
            failure => BadRequest(failure.Message)
        );
    }

    [HttpPut("{sectionName}/{key}")]
    public async Task<IActionResult> UpdateSetting(
        [FromRoute] string sectionName,
        [FromRoute] string key,
        [FromBody] JsonElement value,
        CancellationToken ct)
    {
        var result = await _dispatcher.DispatchAsync(new UpdateSettings(sectionName, key, value), ct);
        return result.Match<IActionResult>(
            success => NoContent(),
            failure => BadRequest(failure.Message)
        );
    }
}
