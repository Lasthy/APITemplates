using Kernel.Entities;

namespace Identity.Entities;

/// <summary>
/// Represents a customer/client entity associated with an <see cref="ApplicationUser"/>.
/// </summary>
public class Client : IAuditable
{
    /// <summary>
    /// Unique identifier for the client (ULID).
    /// </summary>
    public DefaultId Id { get; set; } = DefaultId.NewUlid();

    /// <summary>
    /// Foreign key referencing the associated <see cref="ApplicationUser"/>.
    /// </summary>
    public DefaultId UserId { get; set; }

    /// <summary>
    /// Name of the client / customer.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Contact email address.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Optional contact phone number.
    /// </summary>
    public string? PhoneNumber { get; set; }

    /// <summary>
    /// Optional billing/shipping address.
    /// </summary>
    public string? Address { get; set; }

    /// <summary>
    /// Navigation property to the associated application user.
    /// </summary>
    public ApplicationUser? User { get; set; }

    // ── IAuditable ────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public DateTime CreatedAt { get; set; }

    /// <inheritdoc/>
    public DefaultId? CreatedBy { get; set; }

    /// <inheritdoc/>
    public DateTime? UpdatedAt { get; set; }

    /// <inheritdoc/>
    public DefaultId? UpdatedBy { get; set; }
}
