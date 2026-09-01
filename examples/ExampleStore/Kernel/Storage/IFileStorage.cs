using Kernel.Common;

namespace Kernel.Storage;

/// <summary>
/// Abstraction for storing, retrieving, and managing files and binary blobs.
/// </summary>
public interface IFileStorage
{
    /// <summary>
    /// Uploads content stream to the specified relative or structured storage path.
    /// Returns the normalized storage path on success.
    /// </summary>
    Task<Result<string>> UploadAsync(
        string path,
        Stream content,
        string? contentType = null,
        CancellationToken ct = default);

    /// <summary>
    /// Downloads the file stream from the specified storage path.
    /// </summary>
    Task<Result<Stream>> DownloadAsync(
        string path,
        CancellationToken ct = default);

    /// <summary>
    /// Deletes the file at the specified storage path.
    /// </summary>
    Task<Result> DeleteAsync(
        string path,
        CancellationToken ct = default);

    /// <summary>
    /// Generates a publicly accessible or relative URL for the stored file.
    /// </summary>
    Task<Result<string>> GetUrlAsync(
        string path,
        TimeSpan? expiry = null,
        CancellationToken ct = default);

    /// <summary>
    /// Checks whether a file exists at the specified storage path.
    /// </summary>
    Task<Result<bool>> ExistsAsync(
        string path,
        CancellationToken ct = default);
}
