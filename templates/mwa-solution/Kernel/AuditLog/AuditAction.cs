namespace Kernel.AuditLog;

/// <summary>
/// Defines the type of data mutation captured in an audit log entry.
/// </summary>
public enum AuditAction
{
    /// <summary>
    /// An entity was created / inserted.
    /// </summary>
    Create,

    /// <summary>
    /// An existing entity was modified / updated.
    /// </summary>
    Update,

    /// <summary>
    /// An entity was deleted.
    /// </summary>
    Delete
}
