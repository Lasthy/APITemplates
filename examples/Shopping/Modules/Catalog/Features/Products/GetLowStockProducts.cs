using Catalog.Contracts.Dtos;
using Catalog.Persistence;
using Kernel.Common;
using Kernel.Requests;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Features.Products;

public record GetLowStockProducts(int Page = 1, int PageSize = 20) : IRequest<IReadOnlyList<ProductDto>>;

public class GetLowStockProductsHandler : IRequestHandler<GetLowStockProducts, IReadOnlyList<ProductDto>>
{
    private readonly CatalogDbContext _db;

    public GetLowStockProductsHandler(CatalogDbContext db)
    {
        _db = db;
    }

    public async Task<Result<IReadOnlyList<ProductDto>>> HandleAsync(GetLowStockProducts request, CancellationToken ct = default)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1 ? 20 : request.PageSize;

        var query = _db.Products
            .AsNoTracking()
            .Where(p => p.StockQuantity <= p.LowStockThreshold);

        var items = await query
            .OrderBy(p => p.StockQuantity)
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
