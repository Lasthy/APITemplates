namespace Cms.Contracts.Dtos;

/// <summary>
/// Diff comparison between two revisions of a content item.
/// </summary>
public record RevisionComparisonDto(
    int FromRevision,
    int ToRevision,
    IReadOnlyList<FieldDiff> Changes);

/// <summary>
/// Field-level change between two revisions.
/// </summary>
public record FieldDiff(
    string FieldName,
    string? OldValue,
    string? NewValue);
