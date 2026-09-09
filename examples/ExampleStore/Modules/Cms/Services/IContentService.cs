using Cms.Entities;
using Kernel.Common;

namespace Cms.Services;

/// <summary>
/// Service contract for managing content item lifecycles, revisions, tags, and field validation.
/// </summary>
public interface IContentService
{
    Task<Result<ContentItem>> CreateAsync(
        string contentType,
        string title,
        string? body,
        string? excerpt,
        string? fieldsJson,
        DefaultId authorId,
        IEnumerable<DefaultId>? tagIds = null,
        CancellationToken ct = default);

    Task<Result<ContentItem>> GetByIdAsync(
        DefaultId id,
        CancellationToken ct = default);

    Task<Result<ContentItem>> GetBySlugAsync(
        string contentType,
        string slug,
        CancellationToken ct = default);

    Task<Result> UpdateAsync(
        DefaultId id,
        string? title,
        string? body,
        string? excerpt,
        string? fieldsJson,
        string? metadataJson,
        string? changeNote,
        CancellationToken ct = default);

    Task<Result> DeleteAsync(
        DefaultId id,
        CancellationToken ct = default);

    Task<Result> PublishAsync(
        DefaultId id,
        CancellationToken ct = default);

    Task<Result> UnpublishAsync(
        DefaultId id,
        CancellationToken ct = default);

    Task<Result> ArchiveAsync(
        DefaultId id,
        CancellationToken ct = default);

    Task<Result> RestoreRevisionAsync(
        DefaultId contentId,
        int revisionNumber,
        CancellationToken ct = default);

    Task<Result> SetTagsAsync(
        DefaultId contentId,
        IEnumerable<DefaultId> tagIds,
        CancellationToken ct = default);

    /// <summary>
    /// Validates structured fields against the registered content type schema.
    /// </summary>
    Result ValidateFields(string contentType, string? fieldsJson);
}
