namespace Kernel.AuditLog;

/// <summary>
/// Represents a structured record of an entity mutation in the system.
/// </summary>
public class AuditEntry
{
    /// <summary>
    /// Unique identifier for the audit entry.
    /// </summary>
    public DefaultId Id { get; set; } = DefaultId.NewUlid();

    /// <summary>
    /// The user ID associated with the operation, if authenticated.
    /// </summary>
    public DefaultId? UserId { get; set; }

    /// <summary>
    /// The CLR type name of the mutated entity.
    /// </summary>
    public required string EntityType { get; init; }

    /// <summary>
    /// The primary key identifier of the mutated entity.
    /// </summary>
    public required string EntityId { get; init; }

    /// <summary>
    /// The mutation action performed (Create, Update, Delete).
    /// </summary>
    public required AuditAction Action { get; init; }

    /// <summary>
    /// JSON serialized array of <see cref="AuditPropertyChange"/> capturing property-level diffs.
    /// </summary>
    public string? Changes { get; init; }

    /// <summary>
    /// The correlation ID of the ambient request or operation.
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    /// The UTC timestamp when the mutation occurred.
    /// </summary>
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}
