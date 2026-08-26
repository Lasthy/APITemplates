using Catalog.Contracts.Dtos;
using Catalog.Persistence;
using Kernel.Common;
using Kernel.Requests;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Features.Products;

public record GetProductById(DefaultId Id) : IRequest<ProductDto>;

public class GetProductByIdHandler : IRequestHandler<GetProductById, ProductDto>
{
    private readonly CatalogDbContext _db;

    public GetProductByIdHandler(CatalogDbContext db)
    {
        _db = db;
    }

    public async Task<Result<ProductDto>> HandleAsync(GetProductById request, CancellationToken ct = default)
    {
        var product = await _db.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.Id, ct);

        if (product == null)
        {
            return Result.Failure<ProductDto>(new KeyNotFoundException($"Product with ID '{request.Id}' was not found."));
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
