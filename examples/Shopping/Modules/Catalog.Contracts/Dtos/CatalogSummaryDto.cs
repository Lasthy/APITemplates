namespace Catalog.Contracts.Dtos;

/// <summary>
/// Public summary DTO representing a Catalog resource.
/// Safe to share across module boundaries.
/// </summary>
public record CatalogSummaryDto(DefaultId Id, string Name, bool IsActive);
