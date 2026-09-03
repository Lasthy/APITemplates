using Kernel.Common;

namespace Kernel.Storage;

/// <summary>
/// Abstraction for registering, retrieving, and revoking opaque public identifiers for stored files.
/// </summary>
public interface IFileRegistry
{
    /// <summary>
    /// Registers or retrieves an opaque identifier for the specified storage path.
    /// If an active non-expired mapping already exists and no specific expiry was requested,
    /// returns the existing identifier to prevent duplicate entries.
    /// </summary>
    Task<Result<DefaultId>> RegisterAsync(
        string path,
        TimeSpan? expiry = null,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieves the file registry entry by its opaque identifier.
    /// Returns failure if the entry does not exist or has expired.
    /// </summary>
    Task<Result<FileRegistryEntry>> GetAsync(
        DefaultId id,
        CancellationToken ct = default);

    /// <summary>
    /// Revokes a specific file registry entry by its identifier.
    /// </summary>
    Task<Result> RevokeAsync(
        DefaultId id,
        CancellationToken ct = default);

    /// <summary>
    /// Revokes all file registry entries associated with the specified storage path (e.g. on file deletion).
    /// </summary>
    Task<Result> RevokeByPathAsync(
        string path,
        CancellationToken ct = default);
}
