using Catalog.Contracts.Dtos;
using Catalog.Persistence;
using Kernel.Common;
using Kernel.Requests;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Features.Restock;

public record GetRestockRequests(int Page = 1, int PageSize = 20, string? Status = null) : IRequest<IReadOnlyList<RestockRequestDto>>;

public class GetRestockRequestsHandler : IRequestHandler<GetRestockRequests, IReadOnlyList<RestockRequestDto>>
{
    private readonly CatalogDbContext _db;

    public GetRestockRequestsHandler(CatalogDbContext db)
    {
        _db = db;
    }

    public async Task<Result<IReadOnlyList<RestockRequestDto>>> HandleAsync(GetRestockRequests request, CancellationToken ct = default)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1 ? 20 : request.PageSize;

        var query = _db.RestockRequests.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = request.Status.Trim();
            query = query.Where(r => r.Status == status);
        }

        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new RestockRequestDto(
                r.Id,
                r.ProductId,
                r.ProductName,
                r.RequestedQuantity,
                r.Reason,
                r.Status,
                r.RequestedBy,
                r.CreatedAt,
                r.FulfilledBy,
                r.FulfilledAt))
            .ToListAsync(ct);

        return Result.Success<IReadOnlyList<RestockRequestDto>>(items);
    }
}
