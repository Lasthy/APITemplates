using Catalog.Contracts.Dtos;
using Catalog.Entities;
using Catalog.Persistence;
using Kernel.Common;
using Kernel.Requests;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Features.Products;

public record UpdateProduct(
    DefaultId Id,
    string Name,
    string Description,
    string Sku,
    decimal Price,
    int StockQuantity,
    int LowStockThreshold = 5) : IRequest<ProductDto>;

public class UpdateProductHandler : IRequestHandler<UpdateProduct, ProductDto>
{
    private readonly CatalogDbContext _db;

    public UpdateProductHandler(CatalogDbContext db)
    {
        _db = db;
    }

    public async Task<Result<ProductDto>> HandleAsync(UpdateProduct request, CancellationToken ct = default)
    {
        var product = await _db.Products
            .FirstOrDefaultAsync(p => p.Id == request.Id, ct);

        if (product == null)
        {
            return Result.Failure<ProductDto>(new KeyNotFoundException($"Product with ID '{request.Id}' was not found."));
        }

        if (string.IsNullOrWhiteSpace(request.Name))
            return Result.Failure<ProductDto>(new ArgumentException("Product name is required."));

        if (string.IsNullOrWhiteSpace(request.Sku))
            return Result.Failure<ProductDto>(new ArgumentException("Product SKU is required."));

        if (request.Price < 0)
            return Result.Failure<ProductDto>(new ArgumentException("Product price cannot be negative."));

        if (request.StockQuantity < 0)
            return Result.Failure<ProductDto>(new ArgumentException("Product stock cannot be negative."));

        var skuTrimmed = request.Sku.Trim().ToUpperInvariant();
        if (product.Sku != skuTrimmed)
        {
            var skuExists = await _db.Products
                .AnyAsync(p => p.Sku == skuTrimmed && p.Id != product.Id, ct);

            if (skuExists)
            {
                return Result.Failure<ProductDto>(new InvalidOperationException($"A product with SKU '{request.Sku}' already exists."));
            }
        }

        product.Name = request.Name.Trim();
        product.Description = request.Description.Trim();
        product.Sku = skuTrimmed;
        product.Price = request.Price;
        product.StockQuantity = request.StockQuantity;
        product.LowStockThreshold = request.LowStockThreshold;

        await _db.SaveChangesAsync(ct);

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
