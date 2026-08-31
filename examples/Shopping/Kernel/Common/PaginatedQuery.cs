namespace Kernel.Common;

/// <summary>
/// Base record for paginated queries. Handlers can extend this with additional filters.
/// </summary>
public record PaginatedQuery
{
    /// <summary>
    /// The 1-based page number. Defaults to 1.
    /// </summary>
    public int Page { get; init; } = 1;

    /// <summary>
    /// The number of items per page. Defaults to 20.
    /// </summary>
    public int PageSize { get; init; } = 20;

    /// <summary>
    /// The property name to sort by. Null or whitespace leaves sorting unchanged.
    /// </summary>
    public string? SortBy { get; init; }

    /// <summary>
    /// The direction of sorting. Defaults to <see cref="SortDirection.Ascending"/>.
    /// </summary>
    public SortDirection SortDirection { get; init; } = SortDirection.Ascending;

    /// <summary>
    /// Maximum allowed page size to prevent abuse. Override per-query if needed. Defaults to 100.
    /// </summary>
    public virtual int MaxPageSize => 100;

    /// <summary>
    /// Returns the normalized page index (guaranteed to be at least 1).
    /// </summary>
    public int GetNormalizedPage() => Page < 1 ? 1 : Page;

    /// <summary>
    /// Returns the normalized page size (clamped between 1 and <see cref="MaxPageSize"/>, defaulting to 20 if less than or equal to 0).
    /// </summary>
    public int GetNormalizedPageSize() => PageSize < 1 ? 20 : (PageSize > MaxPageSize ? MaxPageSize : PageSize);
}
