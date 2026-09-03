namespace Kernel.Storage;

/// <summary>
/// Represents a registered public file mapping an opaque identifier to a storage path.
/// </summary>
public class FileRegistryEntry
{
    /// <summary>
    /// The unique random identifier used for public file access.
    /// </summary>
    public DefaultId Id { get; set; } = DefaultId.NewUlid();

    /// <summary>
    /// The internal relative or structured storage path of the file.
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// UTC timestamp when this public mapping was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Optional UTC timestamp when this public mapping expires. If null, the mapping does not expire.
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Whether this public file mapping has expired.
    /// </summary>
    public bool IsExpired => ExpiresAt.HasValue && ExpiresAt.Value <= DateTime.UtcNow;
}
