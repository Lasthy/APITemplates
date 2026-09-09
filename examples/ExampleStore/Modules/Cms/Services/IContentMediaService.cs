using Cms.Entities;
using Kernel.Common;

namespace Cms.Services;

/// <summary>
/// Service contract for media attachment operations backed by the Kernel's IFileStorage.
/// </summary>
public interface IContentMediaService
{
    Task<Result<ContentMedia>> UploadAsync(
        DefaultId? contentItemId,
        Stream fileStream,
        string fileName,
        string mimeType,
        string? altText = null,
        CancellationToken ct = default);

    Task<Result<IEnumerable<ContentMedia>>> GetByContentItemAsync(
        DefaultId contentItemId,
        CancellationToken ct = default);

    Task<Result> DeleteAsync(
        DefaultId mediaId,
        CancellationToken ct = default);

    Task<Result<Stream>> DownloadAsync(
        DefaultId mediaId,
        CancellationToken ct = default);
}
