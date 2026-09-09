using Asp.Versioning;
using Cms.Contracts.Constants;
using Cms.Contracts.Dtos;
using Cms.Features.ContentTypes;
using Kernel.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Cms.Controllers;

/// <summary>
/// Endpoints for discovering and querying registered content types and schemas.
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/cms/types")]
[ApiExplorerSettings(GroupName = "Cms")]
[Tags("Content Types")]
[Authorize(Policy = CmsPolicies.ManageContent)]
public class ContentTypesController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public ContentTypesController(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    /// <summary>
    /// List all registered content types.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ContentTypeDto>>> GetAll(CancellationToken ct)
    {
        var result = await _dispatcher.DispatchAsync(new GetContentTypes(), ct);
        if (result.IsFailure)
        {
            return BadRequest(new { error = result.Messages.FirstOrDefault() ?? result.Error?.Message });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Get content type details with custom field schema by name.
    /// </summary>
    [HttpGet("{name}")]
    public async Task<ActionResult<ContentTypeDetailDto>> GetByName(string name, CancellationToken ct)
    {
        var result = await _dispatcher.DispatchAsync(new GetContentType(name), ct);
        if (result.IsFailure)
        {
            return NotFound(new { error = result.Messages.FirstOrDefault() ?? result.Error?.Message });
        }

        return Ok(result.Value);
    }
}
