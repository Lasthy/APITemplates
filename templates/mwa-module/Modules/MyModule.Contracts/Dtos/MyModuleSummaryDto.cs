namespace MyModule.Contracts.Dtos;

/// <summary>
/// Public summary DTO representing a MyModule resource.
/// Safe to share across module boundaries.
/// </summary>
public record MyModuleSummaryDto(DefaultId Id, string Name, bool IsActive);
