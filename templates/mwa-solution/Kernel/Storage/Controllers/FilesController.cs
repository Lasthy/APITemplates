using Asp.Versioning;
using Kernel.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;

namespace Kernel.Storage.Controllers;

/// <summary>
/// Public file retrieval controller for streaming files by their registered opaque identifier.
/// </summary>
[ApiController]
[ApiVersionNeutral]
[Route("files")]
[AllowAnonymous]
[ApiExplorerSettings(GroupName = "kernel")]
[Tags("Storage")]
public class FilesController : ControllerBase
{
    private static readonly FileExtensionContentTypeProvider ContentTypeProvider = new();

    private readonly IFileRegistry _registry;
    private readonly IFileStorage _storage;
    private readonly FileStorageSettings _settings;

    public FilesController(
        IFileRegistry registry,
        IFileStorage storage,
        FileStorageSettings settings)
    {
        _registry = registry;
        _storage = storage;
        _settings = settings;
    }

    /// <summary>
    /// Retrieves a public file by its opaque registry ID.
    /// </summary>
    /// <param name="id">The opaque unique identifier of the file.</param>
    /// <param name="download">If true, prompts the browser to download the file as an attachment.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFile(
        [FromRoute] string id,
        [FromQuery] bool? download,
        CancellationToken ct)
    {
        if (!_settings.EnablePublicAccess)
        {
            return NotFound();
        }

        if (!DefaultId.TryParse(id, out var fileId))
        {
            return NotFound();
        }

        var entryResult = await _registry.GetAsync(fileId, ct);
        if (entryResult.IsFailure || entryResult.Value is not { } entry)
        {
            return NotFound();
        }

        if (entry.IsExpired)
        {
            return NotFound();
        }

        var downloadResult = await _storage.DownloadAsync(entry.Path, ct);
        if (downloadResult.IsFailure || downloadResult.Value is not { } stream)
        {
            return NotFound();
        }

        if (!ContentTypeProvider.TryGetContentType(entry.Path, out var contentType))
        {
            contentType = "application/octet-stream";
        }

        string? fileDownloadName = download == true ? Path.GetFileName(entry.Path) : null;

        return File(
            fileStream: stream,
            contentType: contentType,
            fileDownloadName: fileDownloadName,
            enableRangeProcessing: true);
    }
}
