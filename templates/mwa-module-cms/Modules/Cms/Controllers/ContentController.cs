using Asp.Versioning;
using Cms.Contracts.Constants;
using Cms.Contracts.Dtos;
using Cms.Features.Content;
using Cms.Features.Revisions;
using Kernel.Common;
using Kernel.Context;
using Kernel.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Cms.Controllers;

/// <summary>
/// Administrative endpoints for content items, revisions, and status transitions.
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/cms/content")]
[ApiExplorerSettings(GroupName = "Cms")]
[Tags("Content")]
[Authorize(Policy = CmsPolicies.ManageContent)]
public class ContentController : ControllerBase
{
    private readonly IDispatcher _dispatcher;
    private readonly IScopeContext _scopeContext;

    public ContentController(IDispatcher dispatcher, IScopeContext scopeContext)
    {
        _dispatcher = dispatcher;
        _scopeContext = scopeContext;
    }

    public record CreateContentRequest(
        string ContentType,
        string Title,
        string? Body = null,
        string? Excerpt = null,
        string? FieldsJson = null,
        List<DefaultId>? TagIds = null);

    public record UpdateContentRequest(
        string? Title = null,
        string? Body = null,
        string? Excerpt = null,
        string? FieldsJson = null,
        string? MetadataJson = null,
        string? ChangeNote = null);

    public record SetTagsRequest(List<DefaultId> TagIds);

    /// <summary>
    /// Create a new content item.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ContentItemDetailDto>> Create(
        [FromBody] CreateContentRequest request,
        CancellationToken ct)
    {
        var authorId = _scopeContext.UserId ?? DefaultId.NewUlid();

        var result = await _dispatcher.DispatchAsync(new CreateContent(
            request.ContentType,
            request.Title,
            request.Body,
            request.Excerpt,
            request.FieldsJson,
            authorId,
            request.TagIds), ct);

        if (result.IsFailure)
        {
            return BadRequest(new { error = result.Messages.FirstOrDefault() ?? result.Error?.Message });
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    /// <summary>
    /// List all content items (drafts, published, archived) with optional filters and pagination.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PaginatedResult<ContentItemDto>>> GetList(
        [FromQuery] string? contentType = null,
        [FromQuery] string? status = null,
        [FromQuery] string? tag = null,
        [FromQuery] DefaultId? authorId = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? sortBy = null,
        [FromQuery] SortDirection sortDirection = SortDirection.Descending,
        CancellationToken ct = default)
    {
        var result = await _dispatcher.DispatchAsync(new GetContents(
            contentType,
            status,
            tag,
            authorId,
            fromDate,
            toDate,
            page,
            pageSize,
            sortBy,
            sortDirection), ct);

        if (result.IsFailure)
        {
            return BadRequest(new { error = result.Messages.FirstOrDefault() ?? result.Error?.Message });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Get content item details by ID.
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<ContentItemDetailDto>> GetById(DefaultId id, CancellationToken ct)
    {
        var result = await _dispatcher.DispatchAsync(new GetContent(id), ct);
        if (result.IsFailure)
        {
            return NotFound(new { error = result.Messages.FirstOrDefault() ?? result.Error?.Message });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Update a content item, incrementing revision number and recording revision history.
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<ContentItemDetailDto>> Update(
        DefaultId id,
        [FromBody] UpdateContentRequest request,
        CancellationToken ct)
    {
        var result = await _dispatcher.DispatchAsync(new UpdateContent(
            id,
            request.Title,
            request.Body,
            request.Excerpt,
            request.FieldsJson,
            request.MetadataJson,
            request.ChangeNote), ct);

        if (result.IsFailure)
        {
            return BadRequest(new { error = result.Messages.FirstOrDefault() ?? result.Error?.Message });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Soft-delete a content item.
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(DefaultId id, CancellationToken ct)
    {
        var result = await _dispatcher.DispatchAsync(new DeleteContent(id), ct);
        if (result.IsFailure)
        {
            return NotFound(new { error = result.Messages.FirstOrDefault() ?? result.Error?.Message });
        }

        return NoContent();
    }

    /// <summary>
    /// Publish a draft or archived content item.
    /// </summary>
    [HttpPost("{id}/publish")]
    [Authorize(Policy = CmsPolicies.PublishContent)]
    public async Task<IActionResult> Publish(DefaultId id, CancellationToken ct)
    {
        var result = await _dispatcher.DispatchAsync(new PublishContent(id), ct);
        if (result.IsFailure)
        {
            return BadRequest(new { error = result.Messages.FirstOrDefault() ?? result.Error?.Message });
        }

        return Ok(new { success = true });
    }

    /// <summary>
    /// Revert a published content item back to draft.
    /// </summary>
    [HttpPost("{id}/unpublish")]
    [Authorize(Policy = CmsPolicies.PublishContent)]
    public async Task<IActionResult> Unpublish(DefaultId id, CancellationToken ct)
    {
        var result = await _dispatcher.DispatchAsync(new UnpublishContent(id), ct);
        if (result.IsFailure)
        {
            return BadRequest(new { error = result.Messages.FirstOrDefault() ?? result.Error?.Message });
        }

        return Ok(new { success = true });
    }

    /// <summary>
    /// Archive a content item.
    /// </summary>
    [HttpPost("{id}/archive")]
    [Authorize(Policy = CmsPolicies.PublishContent)]
    public async Task<IActionResult> Archive(DefaultId id, CancellationToken ct)
    {
        var result = await _dispatcher.DispatchAsync(new ArchiveContent(id), ct);
        if (result.IsFailure)
        {
            return BadRequest(new { error = result.Messages.FirstOrDefault() ?? result.Error?.Message });
        }

        return Ok(new { success = true });
    }

    /// <summary>
    /// Set the tags assigned to a content item.
    /// </summary>
    [HttpPut("{id}/tags")]
    public async Task<IActionResult> SetTags(
        DefaultId id,
        [FromBody] SetTagsRequest request,
        CancellationToken ct)
    {
        var result = await _dispatcher.DispatchAsync(new SetContentTags(id, request.TagIds), ct);
        if (result.IsFailure)
        {
            return BadRequest(new { error = result.Messages.FirstOrDefault() ?? result.Error?.Message });
        }

        return Ok(new { success = true });
    }

    /// <summary>
    /// List revisions for a content item.
    /// </summary>
    [HttpGet("{id}/revisions")]
    public async Task<ActionResult<IReadOnlyList<ContentRevisionDto>>> GetRevisions(DefaultId id, CancellationToken ct)
    {
        var result = await _dispatcher.DispatchAsync(new GetRevisions(id), ct);
        if (result.IsFailure)
        {
            return NotFound(new { error = result.Messages.FirstOrDefault() ?? result.Error?.Message });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Get snapshot details for a specific revision number.
    /// </summary>
    [HttpGet("{id}/revisions/{number}")]
    public async Task<ActionResult<ContentRevisionDetailDto>> GetRevision(DefaultId id, int number, CancellationToken ct)
    {
        var result = await _dispatcher.DispatchAsync(new GetRevision(id, number), ct);
        if (result.IsFailure)
        {
            return NotFound(new { error = result.Messages.FirstOrDefault() ?? result.Error?.Message });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Restore a content item to a specific historical revision.
    /// </summary>
    [HttpPost("{id}/revisions/{number}/restore")]
    public async Task<ActionResult<ContentItemDetailDto>> RestoreRevision(DefaultId id, int number, CancellationToken ct)
    {
        var result = await _dispatcher.DispatchAsync(new RestoreRevision(id, number), ct);
        if (result.IsFailure)
        {
            return BadRequest(new { error = result.Messages.FirstOrDefault() ?? result.Error?.Message });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Compare two revisions of a content item to produce a field-by-field diff.
    /// </summary>
    [HttpGet("{id}/revisions/compare")]
    public async Task<ActionResult<RevisionComparisonDto>> CompareRevisions(
        DefaultId id,
        [FromQuery] int from,
        [FromQuery] int to,
        CancellationToken ct)
    {
        var result = await _dispatcher.DispatchAsync(new CompareRevisions(id, from, to), ct);
        if (result.IsFailure)
        {
            return BadRequest(new { error = result.Messages.FirstOrDefault() ?? result.Error?.Message });
        }

        return Ok(result.Value);
    }
}
