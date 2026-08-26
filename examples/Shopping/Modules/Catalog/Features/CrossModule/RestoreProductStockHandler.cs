using Catalog.Contracts.Requests;
using Catalog.Persistence;
using Kernel.Common;
using Kernel.Requests;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Features.CrossModule;

public class RestoreProductStockHandler : IRequestHandler<RestoreProductStock, bool>
{
    private readonly CatalogDbContext _db;

    public RestoreProductStockHandler(CatalogDbContext db)
    {
        _db = db;
    }

    public async Task<Result<bool>> HandleAsync(RestoreProductStock request, CancellationToken ct = default)
    {
        if (request.Items == null || request.Items.Count == 0)
        {
            return Result.Success(true);
        }

        var productIds = request.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = await _db.Products
            .Where(p => productIds.Contains(p.Id))
            .ToListAsync(ct);

        foreach (var item in request.Items)
        {
            var product = products.FirstOrDefault(p => p.Id == item.ProductId);
            if (product != null)
            {
                product.StockQuantity += item.Quantity;
            }
        }

        await _db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
