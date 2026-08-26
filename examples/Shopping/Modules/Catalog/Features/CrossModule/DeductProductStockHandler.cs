using Catalog.Contracts.Requests;
using Catalog.Persistence;
using Kernel.Common;
using Kernel.Requests;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Features.CrossModule;

public class DeductProductStockHandler : IRequestHandler<DeductProductStock, bool>
{
    private readonly CatalogDbContext _db;

    public DeductProductStockHandler(CatalogDbContext db)
    {
        _db = db;
    }

    public async Task<Result<bool>> HandleAsync(DeductProductStock request, CancellationToken ct = default)
    {
        if (request.Items == null || request.Items.Count == 0)
        {
            return Result.Success(true);
        }

        var productIds = request.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = await _db.Products
            .Where(p => productIds.Contains(p.Id))
            .ToListAsync(ct);

        // Verify all products exist and have sufficient stock
        foreach (var item in request.Items)
        {
            var product = products.FirstOrDefault(p => p.Id == item.ProductId);
            if (product == null)
            {
                return Result.Failure<bool>(new KeyNotFoundException($"Product with ID '{item.ProductId}' was not found."));
            }

            if (product.StockQuantity < item.Quantity)
            {
                return Result.Failure<bool>(new InvalidOperationException(
                    $"Insufficient stock for product '{product.Name}'. Available: {product.StockQuantity}, Requested: {item.Quantity}"));
            }
        }

        // Deduct quantities
        foreach (var item in request.Items)
        {
            var product = products.First(p => p.Id == item.ProductId);
            product.StockQuantity -= item.Quantity;
        }

        await _db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
