using Asp.Versioning;
using Cms.Contracts.Constants;
using Cms.Contracts.Dtos;
using Cms.Features.Tags;
using Kernel.Common;
using Kernel.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Cms.Controllers;

/// <summary>
/// Endpoints for managing content tags.
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/cms/tags")]
[ApiExplorerSettings(GroupName = "Cms")]
[Tags("Tags")]
[Authorize(Policy = CmsPolicies.ManageContent)]
public class TagsController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public TagsController(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public record CreateTagRequest(string Name, string? Slug = null, string? Description = null);
    public record UpdateTagRequest(string? Name = null, string? Description = null);

    /// <summary>
    /// Create a new tag.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ContentTagDto>> Create([FromBody] CreateTagRequest request, CancellationToken ct)
    {
        var result = await _dispatcher.DispatchAsync(new CreateTag(request.Name, request.Slug, request.Description), ct);
        if (result.IsFailure)
        {
            return BadRequest(new { error = result.Messages.FirstOrDefault() ?? result.Error?.Message });
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    /// <summary>
    /// List all tags with pagination.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PaginatedResult<ContentTagDto>>> GetList(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _dispatcher.DispatchAsync(new GetTags(page, pageSize), ct);
        if (result.IsFailure)
        {
            return BadRequest(new { error = result.Messages.FirstOrDefault() ?? result.Error?.Message });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Get tag by ID.
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<ContentTagDto>> GetById(DefaultId id, CancellationToken ct)
    {
        var result = await _dispatcher.DispatchAsync(new GetTag(id), ct);
        if (result.IsFailure)
        {
            return NotFound(new { error = result.Messages.FirstOrDefault() ?? result.Error?.Message });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Update tag name and description.
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<ContentTagDto>> Update(DefaultId id, [FromBody] UpdateTagRequest request, CancellationToken ct)
    {
        var result = await _dispatcher.DispatchAsync(new UpdateTag(id, request.Name, request.Description), ct);
        if (result.IsFailure)
        {
            return BadRequest(new { error = result.Messages.FirstOrDefault() ?? result.Error?.Message });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Delete tag, removing it from all associated content items.
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(DefaultId id, CancellationToken ct)
    {
        var result = await _dispatcher.DispatchAsync(new DeleteTag(id), ct);
        if (result.IsFailure)
        {
            return NotFound(new { error = result.Messages.FirstOrDefault() ?? result.Error?.Message });
        }

        return NoContent();
    }
}
