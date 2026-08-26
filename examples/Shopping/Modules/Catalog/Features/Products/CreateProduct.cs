using Catalog.Contracts.Dtos;
using Catalog.Contracts.Events;
using Catalog.Entities;
using Catalog.Persistence;
using Kernel.Common;
using Kernel.Events;
using Kernel.Requests;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Features.Products;

public record CreateProduct(
    string Name,
    string Description,
    string Sku,
    decimal Price,
    int StockQuantity,
    int LowStockThreshold = 5) : IRequest<ProductDto>;

public class CreateProductHandler : IRequestHandler<CreateProduct, ProductDto>
{
    private readonly CatalogDbContext _db;
    private readonly IEventBus _eventBus;

    public CreateProductHandler(CatalogDbContext db, IEventBus eventBus)
    {
        _db = db;
        _eventBus = eventBus;
    }

    public async Task<Result<ProductDto>> HandleAsync(CreateProduct request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return Result.Failure<ProductDto>(new ArgumentException("Product name is required."));

        if (string.IsNullOrWhiteSpace(request.Sku))
            return Result.Failure<ProductDto>(new ArgumentException("Product SKU is required."));

        if (request.Price < 0)
            return Result.Failure<ProductDto>(new ArgumentException("Product price cannot be negative."));

        if (request.StockQuantity < 0)
            return Result.Failure<ProductDto>(new ArgumentException("Product stock cannot be negative."));

        var skuExists = await _db.Products
            .AnyAsync(p => p.Sku.ToLower() == request.Sku.Trim().ToLower(), ct);

        if (skuExists)
        {
            return Result.Failure<ProductDto>(new InvalidOperationException($"A product with SKU '{request.Sku}' already exists."));
        }

        var product = new Product
        {
            Name = request.Name.Trim(),
            Description = request.Description.Trim(),
            Sku = request.Sku.Trim().ToUpperInvariant(),
            Price = request.Price,
            StockQuantity = request.StockQuantity,
            LowStockThreshold = request.LowStockThreshold
        };

        _db.Products.Add(product);
        await _db.SaveChangesAsync(ct);

        await _eventBus.PublishAsync(new ProductCreatedEvent(
            product.Id,
            product.Name,
            product.Sku,
            product.Price,
            product.StockQuantity), ct);

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
