using Asp.Versioning;
using Cms.Contracts.Constants;
using Cms.Contracts.Dtos;
using Cms.Features.Media;
using Kernel.Common;
using Kernel.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Cms.Controllers;

/// <summary>
/// Endpoints for uploading, retrieving, and deleting media files.
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/cms/media")]
[ApiExplorerSettings(GroupName = "Cms")]
[Tags("Media")]
[Authorize(Policy = CmsPolicies.ManageContent)]
public class MediaController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public MediaController(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    /// <summary>
    /// Upload a media file, optionally attaching it to a content item.
    /// </summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ContentMediaDto>> Upload(
        IFormFile file,
        [FromQuery] DefaultId? contentItemId = null,
        [FromForm] string? altText = null,
        CancellationToken ct = default)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { error = "A non-empty file must be provided." });
        }

        using var stream = file.OpenReadStream();
        var mimeType = !string.IsNullOrWhiteSpace(file.ContentType) ? file.ContentType : "application/octet-stream";

        var result = await _dispatcher.DispatchAsync(new UploadMedia(
            contentItemId,
            stream,
            file.FileName,
            mimeType,
            altText), ct);

        if (result.IsFailure)
        {
            return BadRequest(new { error = result.Messages.FirstOrDefault() ?? result.Error?.Message });
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    /// <summary>
    /// List media records with optional filters (by content item or MIME type) and pagination.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PaginatedResult<ContentMediaDto>>> GetList(
        [FromQuery] DefaultId? contentItemId = null,
        [FromQuery] string? mimeType = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _dispatcher.DispatchAsync(new GetMedia(contentItemId, mimeType, page, pageSize), ct);
        if (result.IsFailure)
        {
            return BadRequest(new { error = result.Messages.FirstOrDefault() ?? result.Error?.Message });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Get media metadata by ID.
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<ContentMediaDto>> GetById(DefaultId id, CancellationToken ct)
    {
        var result = await _dispatcher.DispatchAsync(new GetMediaItem(id), ct);
        if (result.IsFailure)
        {
            return NotFound(new { error = result.Messages.FirstOrDefault() ?? result.Error?.Message });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Delete media record and remove the stored file.
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(DefaultId id, CancellationToken ct)
    {
        var result = await _dispatcher.DispatchAsync(new DeleteMedia(id), ct);
        if (result.IsFailure)
        {
            return NotFound(new { error = result.Messages.FirstOrDefault() ?? result.Error?.Message });
        }

        return NoContent();
    }

    /// <summary>
    /// List all media attachments for a specific content item.
    /// </summary>
    [HttpGet("content/{contentId}")]
    public async Task<ActionResult<IReadOnlyList<ContentMediaDto>>> GetByContent(DefaultId contentId, CancellationToken ct)
    {
        var result = await _dispatcher.DispatchAsync(new GetContentMedia(contentId), ct);
        if (result.IsFailure)
        {
            return NotFound(new { error = result.Messages.FirstOrDefault() ?? result.Error?.Message });
        }

        return Ok(result.Value);
    }
}
