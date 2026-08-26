using Catalog.Contracts.Dtos;
using Catalog.Persistence;
using Kernel.Common;
using Kernel.Requests;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Features.Products;

public record GetProducts(int Page = 1, int PageSize = 20, string? Search = null) : IRequest<IReadOnlyList<ProductDto>>;

public class GetProductsHandler : IRequestHandler<GetProducts, IReadOnlyList<ProductDto>>
{
    private readonly CatalogDbContext _db;

    public GetProductsHandler(CatalogDbContext db)
    {
        _db = db;
    }

    public async Task<Result<IReadOnlyList<ProductDto>>> HandleAsync(GetProducts request, CancellationToken ct = default)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1 ? 20 : request.PageSize;

        var query = _db.Products.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLower();
            query = query.Where(p => p.Name.ToLower().Contains(search) || p.Sku.ToLower().Contains(search));
        }

        var items = await query
            .OrderBy(p => p.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new ProductDto(
                p.Id,
                p.Name,
                p.Description,
                p.Sku,
                p.Price,
                p.StockQuantity,
                p.LowStockThreshold,
                p.IsLowStock,
                p.CreatedAt))
            .ToListAsync(ct);

        return Result.Success<IReadOnlyList<ProductDto>>(items);
    }
}
