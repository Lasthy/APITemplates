namespace Orders.Contracts.Dtos;

/// <summary>
/// Public summary DTO representing a Orders resource.
/// Safe to share across module boundaries.
/// </summary>
public record OrdersSummaryDto(DefaultId Id, string Name, bool IsActive);
