namespace Kernel.Common;

/// <summary>
/// Represents the paginated result of an operation containing a collection of items and pagination metadata.
/// </summary>
/// <typeparam name="T">Type of the items in the paginated collection.</typeparam>
public record PaginatedResult<T> : Result<IEnumerable<T>>
{
    /// <summary>
    /// Current 1-based page number.
    /// </summary>
    public int Page { get; }

    /// <summary>
    /// Number of items per page.
    /// </summary>
    public int PageSize { get; }

    /// <summary>
    /// Total number of items matching the query across all pages.
    /// </summary>
    public int TotalCount { get; }

    /// <summary>
    /// Total number of available pages based on <see cref="TotalCount"/> and <see cref="PageSize"/>.
    /// </summary>
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;

    /// <summary>
    /// Indicates whether there is a subsequent page of results.
    /// </summary>
    public bool HasNextPage => Page < TotalPages;

    /// <summary>
    /// Indicates whether there is a preceding page of results.
    /// </summary>
    public bool HasPreviousPage => Page > 1;

    /// <summary>
    /// Convenience property to access items as non-null enumerable.
    /// </summary>
    public IEnumerable<T> Items => Value ?? [];

    internal PaginatedResult(
        IEnumerable<T>? value,
        int page,
        int pageSize,
        int totalCount,
        bool isSuccess,
        Exception? error = null,
        params string[] messages) : base(value, isSuccess, error, messages)
    {
        Page = page;
        PageSize = pageSize;
        TotalCount = totalCount;
    }

    internal PaginatedResult(
        int page,
        IEnumerable<T> value,
        bool isSuccess,
        Exception? error = null,
        params string[] messages) : this(value, page, value?.Count() ?? 0, value?.Count() ?? 0, isSuccess, error, messages)
    {
    }

    /// <summary>
    /// Creates a successful paginated result.
    /// </summary>
    public static PaginatedResult<T> Create(
        IEnumerable<T> items,
        int page,
        int pageSize,
        int totalCount,
        params string[] messages)
    {
        return new PaginatedResult<T>(items, page, pageSize, totalCount, true, null, messages);
    }

    /// <summary>
    /// Creates a successful paginated result (alias for <see cref="Create"/>).
    /// </summary>
    public static PaginatedResult<T> Success(
        IEnumerable<T> items,
        int page,
        int pageSize,
        int totalCount,
        params string[] messages)
    {
        return Create(items, page, pageSize, totalCount, messages);
    }

    /// <summary>
    /// Creates an empty paginated result.
    /// </summary>
    public static PaginatedResult<T> Empty(int page = 1, int pageSize = 20)
    {
        return new PaginatedResult<T>([], page, pageSize, 0, true);
    }

    /// <summary>
    /// Creates a failure paginated result.
    /// </summary>
    public static new PaginatedResult<T> Failure(Exception error, params string[] messages)
    {
        return new PaginatedResult<T>(null, 0, 0, 0, false, error, messages);
    }
}
