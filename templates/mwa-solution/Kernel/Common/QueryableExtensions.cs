using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;

namespace Kernel.Common;

/// <summary>
/// Extension methods for applying sorting, pagination, and paginated result conversions to <see cref="IQueryable{T}"/> and <see cref="IEnumerable{T}"/>.
/// </summary>
public static class QueryableExtensions
{
    /// <summary>
    /// Applies dynamic sorting by property name and direction to a queryable.
    /// Supports nested properties (e.g. "Address.City") and is case-insensitive.
    /// If the property is not found or <paramref name="sortBy"/> is null/empty, returns the query unchanged.
    /// </summary>
    public static IQueryable<T> ApplySort<T>(
        this IQueryable<T> query,
        string? sortBy,
        SortDirection direction = SortDirection.Ascending)
    {
        if (string.IsNullOrWhiteSpace(sortBy))
            return query;

        var entityType = typeof(T);
        var parameter = Expression.Parameter(entityType, "x");
        Expression memberAccess = parameter;

        var propertyParts = sortBy.Trim().Split('.');
        foreach (var part in propertyParts)
        {
            var property = memberAccess.Type.GetProperty(
                part,
                BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);

            if (property == null)
                return query;

            memberAccess = Expression.MakeMemberAccess(memberAccess, property);
        }

        var orderByLambda = Expression.Lambda(memberAccess, parameter);
        var methodName = direction == SortDirection.Descending ? "OrderByDescending" : "OrderBy";

        var resultExpression = Expression.Call(
            typeof(Queryable),
            methodName,
            [entityType, memberAccess.Type],
            query.Expression,
            Expression.Quote(orderByLambda));

        return query.Provider.CreateQuery<T>(resultExpression);
    }

    /// <summary>
    /// Applies dynamic sorting specified by <see cref="PaginatedQuery.SortBy"/> and <see cref="PaginatedQuery.SortDirection"/>.
    /// </summary>
    public static IQueryable<T> ApplySort<T>(this IQueryable<T> query, PaginatedQuery pagination)
    {
        ArgumentNullException.ThrowIfNull(pagination);
        return query.ApplySort(pagination.SortBy, pagination.SortDirection);
    }

    /// <summary>
    /// Applies pagination (Skip / Take) to a queryable using explicit page parameters.
    /// </summary>
    public static IQueryable<T> ApplyPagination<T>(
        this IQueryable<T> query,
        int page,
        int pageSize,
        int maxPageSize = 100)
    {
        var normalizedPage = Math.Max(1, page);
        var normalizedPageSize = pageSize < 1 ? 20 : (pageSize > maxPageSize ? maxPageSize : pageSize);
        var skip = (normalizedPage - 1) * normalizedPageSize;
        return query.Skip(skip).Take(normalizedPageSize);
    }

    /// <summary>
    /// Applies dynamic sorting (if <see cref="PaginatedQuery.SortBy"/> is present) and pagination to a queryable.
    /// </summary>
    public static IQueryable<T> ApplyPagination<T>(this IQueryable<T> query, PaginatedQuery pagination)
    {
        ArgumentNullException.ThrowIfNull(pagination);
        if (!string.IsNullOrWhiteSpace(pagination.SortBy))
        {
            query = query.ApplySort(pagination.SortBy, pagination.SortDirection);
        }
        return query.ApplyPagination(pagination.Page, pagination.PageSize, pagination.MaxPageSize);
    }

    /// <summary>
    /// Materializes the query asynchronously into a <see cref="PaginatedResult{T}"/> containing total count and paginated items.
    /// </summary>
    public static async Task<PaginatedResult<T>> ToPaginatedResultAsync<T>(
        this IQueryable<T> query,
        PaginatedQuery pagination,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(pagination);
        var normalizedPage = pagination.GetNormalizedPage();
        var normalizedPageSize = pagination.GetNormalizedPageSize();

        var totalCount = await ExecuteCountAsync(query, ct);
        var paginatedQuery = query.ApplyPagination(pagination);
        var items = await ExecuteToListAsync(paginatedQuery, ct);

        return PaginatedResult<T>.Create(items, normalizedPage, normalizedPageSize, totalCount);
    }

    /// <summary>
    /// Materializes the query asynchronously into a <see cref="PaginatedResult{T}"/> using explicit page parameters.
    /// </summary>
    public static async Task<PaginatedResult<T>> ToPaginatedResultAsync<T>(
        this IQueryable<T> query,
        int page,
        int pageSize,
        int maxPageSize = 100,
        CancellationToken ct = default)
    {
        var normalizedPage = Math.Max(1, page);
        var normalizedPageSize = pageSize < 1 ? 20 : (pageSize > maxPageSize ? maxPageSize : pageSize);

        var totalCount = await ExecuteCountAsync(query, ct);
        var paginatedQuery = query.ApplyPagination(page, pageSize, maxPageSize);
        var items = await ExecuteToListAsync(paginatedQuery, ct);

        return PaginatedResult<T>.Create(items, normalizedPage, normalizedPageSize, totalCount);
    }

    /// <summary>
    /// Materializes the query synchronously into a <see cref="PaginatedResult{T}"/>.
    /// </summary>
    public static PaginatedResult<T> ToPaginatedResult<T>(this IQueryable<T> query, PaginatedQuery pagination)
    {
        ArgumentNullException.ThrowIfNull(pagination);
        var normalizedPage = pagination.GetNormalizedPage();
        var normalizedPageSize = pagination.GetNormalizedPageSize();

        var totalCount = query.Count();
        var items = query.ApplyPagination(pagination).ToList();

        return PaginatedResult<T>.Create(items, normalizedPage, normalizedPageSize, totalCount);
    }

    /// <summary>
    /// Materializes the query synchronously into a <see cref="PaginatedResult{T}"/> using explicit page parameters.
    /// </summary>
    public static PaginatedResult<T> ToPaginatedResult<T>(
        this IQueryable<T> query,
        int page,
        int pageSize,
        int maxPageSize = 100)
    {
        var normalizedPage = Math.Max(1, page);
        var normalizedPageSize = pageSize < 1 ? 20 : (pageSize > maxPageSize ? maxPageSize : pageSize);

        var totalCount = query.Count();
        var items = query.ApplyPagination(page, pageSize, maxPageSize).ToList();

        return PaginatedResult<T>.Create(items, normalizedPage, normalizedPageSize, totalCount);
    }

    /// <summary>
    /// Applies dynamic sorting to an <see cref="IEnumerable{T}"/>.
    /// </summary>
    public static IEnumerable<T> ApplySort<T>(
        this IEnumerable<T> source,
        string? sortBy,
        SortDirection direction = SortDirection.Ascending)
    {
        return source.AsQueryable().ApplySort(sortBy, direction).AsEnumerable();
    }

    /// <summary>
    /// Applies dynamic sorting specified by <see cref="PaginatedQuery"/> to an <see cref="IEnumerable{T}"/>.
    /// </summary>
    public static IEnumerable<T> ApplySort<T>(this IEnumerable<T> source, PaginatedQuery pagination)
    {
        return source.AsQueryable().ApplySort(pagination).AsEnumerable();
    }

    /// <summary>
    /// Applies pagination to an <see cref="IEnumerable{T}"/>.
    /// </summary>
    public static IEnumerable<T> ApplyPagination<T>(this IEnumerable<T> source, PaginatedQuery pagination)
    {
        return source.AsQueryable().ApplyPagination(pagination).AsEnumerable();
    }

    /// <summary>
    /// Applies pagination to an <see cref="IEnumerable{T}"/> using explicit page parameters.
    /// </summary>
    public static IEnumerable<T> ApplyPagination<T>(
        this IEnumerable<T> source,
        int page,
        int pageSize,
        int maxPageSize = 100)
    {
        return source.AsQueryable().ApplyPagination(page, pageSize, maxPageSize).AsEnumerable();
    }

    /// <summary>
    /// Converts an <see cref="IEnumerable{T}"/> into a <see cref="PaginatedResult{T}"/>.
    /// </summary>
    public static PaginatedResult<T> ToPaginatedResult<T>(this IEnumerable<T> source, PaginatedQuery pagination)
    {
        ArgumentNullException.ThrowIfNull(pagination);
        var list = source as IReadOnlyList<T> ?? source.ToList();
        var totalCount = list.Count;
        var normalizedPage = pagination.GetNormalizedPage();
        var normalizedPageSize = pagination.GetNormalizedPageSize();

        var items = list.AsQueryable().ApplyPagination(pagination).ToList();
        return PaginatedResult<T>.Create(items, normalizedPage, normalizedPageSize, totalCount);
    }

    /// <summary>
    /// Converts an <see cref="IEnumerable{T}"/> into a <see cref="PaginatedResult{T}"/> using explicit page parameters.
    /// </summary>
    public static PaginatedResult<T> ToPaginatedResult<T>(
        this IEnumerable<T> source,
        int page,
        int pageSize,
        int maxPageSize = 100)
    {
        var list = source as IReadOnlyList<T> ?? source.ToList();
        var totalCount = list.Count;
        var normalizedPage = Math.Max(1, page);
        var normalizedPageSize = pageSize < 1 ? 20 : (pageSize > maxPageSize ? maxPageSize : pageSize);

        var items = list.AsQueryable().ApplyPagination(page, pageSize, maxPageSize).ToList();
        return PaginatedResult<T>.Create(items, normalizedPage, normalizedPageSize, totalCount);
    }

    private static async Task<int> ExecuteCountAsync<T>(IQueryable<T> query, CancellationToken ct)
    {
        if (query is IAsyncEnumerable<T>)
        {
            return await EntityFrameworkQueryableExtensions.CountAsync(query, ct);
        }
        return query.Count();
    }

    private static async Task<List<T>> ExecuteToListAsync<T>(IQueryable<T> query, CancellationToken ct)
    {
        if (query is IAsyncEnumerable<T>)
        {
            return await EntityFrameworkQueryableExtensions.ToListAsync(query, ct);
        }
        return query.ToList();
    }
}
