using Kernel.Entities;

namespace Catalog.Entities;

/// <summary>
/// Status constants for product restock requests.
/// </summary>
public static class RestockStatus
{
    public const string Pending = "Pending";
    public const string Fulfilled = "Fulfilled";
    public const string Rejected = "Rejected";
}

/// <summary>
/// Represents a restock request submitted by an employee when store stock is low.
/// </summary>
public class RestockRequest : IAuditable
{
    public DefaultId Id { get; set; } = DefaultId.NewUlid();
    public DefaultId ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int RequestedQuantity { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = RestockStatus.Pending;

    public DefaultId RequestedBy { get; set; }
    public DefaultId? FulfilledBy { get; set; }
    public DateTime? FulfilledAt { get; set; }

    // IAuditable implementation
    public DateTime CreatedAt { get; set; }
    public DefaultId? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DefaultId? UpdatedBy { get; set; }
}
