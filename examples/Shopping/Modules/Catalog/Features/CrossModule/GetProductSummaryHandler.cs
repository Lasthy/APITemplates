using Catalog.Contracts.Dtos;
using Catalog.Contracts.Requests;
using Catalog.Persistence;
using Kernel.Common;
using Kernel.Requests;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Features.CrossModule;

public class GetProductSummaryHandler : IRequestHandler<GetProductSummary, ProductDto>
{
    private readonly CatalogDbContext _db;

    public GetProductSummaryHandler(CatalogDbContext db)
    {
        _db = db;
    }

    public async Task<Result<ProductDto>> HandleAsync(GetProductSummary request, CancellationToken ct = default)
    {
        var product = await _db.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.ProductId, ct);

        if (product == null)
        {
            return Result.Failure<ProductDto>(new KeyNotFoundException($"Product with ID '{request.ProductId}' was not found."));
        }

        var dto = new ProductDto(
            product.Id,
            product.Name,
            product.Description,
            product.Sku,
            product.Price,
            product.StockQuantity,
            product.LowStockThreshold,
            product.IsLowStock,
            product.CreatedAt);

        return Result.Success(dto);
    }
}
