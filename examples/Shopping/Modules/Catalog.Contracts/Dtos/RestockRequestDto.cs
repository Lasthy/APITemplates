namespace Catalog.Contracts.Dtos;

/// <summary>
/// Data transfer object for restock request information.
/// </summary>
public record RestockRequestDto(
    DefaultId Id,
    DefaultId ProductId,
    string ProductName,
    int RequestedQuantity,
    string Reason,
    string Status,
    DefaultId RequestedBy,
    DateTime CreatedAt,
    DefaultId? FulfilledBy = null,
    DateTime? FulfilledAt = null);
