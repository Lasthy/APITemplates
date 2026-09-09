using Asp.Versioning;
using Cms.Contracts.Dtos;
using Cms.Features.Public;
using Kernel.Common;
using Kernel.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Cms.Controllers;

/// <summary>
/// Public read-only endpoints for accessing published content items and tags.
/// No authentication required.
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/cms/public")]
[ApiExplorerSettings(GroupName = "Cms")]
[Tags("Public Content")]
[AllowAnonymous]
public class ContentPublicController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public ContentPublicController(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    /// <summary>
    /// List published content items with optional type and tag filters.
    /// </summary>
    [HttpGet("content")]
    public async Task<ActionResult<PaginatedResult<ContentItemDto>>> GetPublishedContent(
        [FromQuery] string? type = null,
        [FromQuery] string? tag = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _dispatcher.DispatchAsync(new GetPublishedContents(type, tag, page, pageSize), ct);
        if (result.IsFailure)
        {
            return BadRequest(new { error = result.Messages.FirstOrDefault() ?? result.Error?.Message });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Get published content item by content type and slug.
    /// </summary>
    [HttpGet("content/{type}/{slug}")]
    public async Task<ActionResult<ContentItemDetailDto>> GetPublishedContentBySlug(
        string type,
        string slug,
        CancellationToken ct)
    {
        var result = await _dispatcher.DispatchAsync(new GetPublishedContentBySlug(type, slug), ct);
        if (result.IsFailure)
        {
            return NotFound(new { error = result.Messages.FirstOrDefault() ?? result.Error?.Message });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// List all tags with their respective published content counts.
    /// </summary>
    [HttpGet("tags")]
    public async Task<ActionResult<IReadOnlyList<PublicTagDto>>> GetTags(CancellationToken ct)
    {
        var result = await _dispatcher.DispatchAsync(new GetPublicTags(), ct);
        if (result.IsFailure)
        {
            return BadRequest(new { error = result.Messages.FirstOrDefault() ?? result.Error?.Message });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Get tag details along with published content items having this tag.
    /// </summary>
    [HttpGet("tags/{slug}")]
    public async Task<ActionResult<PublicTagDetailDto>> GetTagBySlug(string slug, CancellationToken ct)
    {
        var result = await _dispatcher.DispatchAsync(new GetPublicTagBySlug(slug), ct);
        if (result.IsFailure)
        {
            return NotFound(new { error = result.Messages.FirstOrDefault() ?? result.Error?.Message });
        }

        return Ok(result.Value);
    }
}
