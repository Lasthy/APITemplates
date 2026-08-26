using Catalog.Persistence;
using Kernel.Common;
using Kernel.Requests;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Features.Products;

public record DeleteProduct(DefaultId Id) : IRequest<bool>;

public class DeleteProductHandler : IRequestHandler<DeleteProduct, bool>
{
    private readonly CatalogDbContext _db;

    public DeleteProductHandler(CatalogDbContext db)
    {
        _db = db;
    }

    public async Task<Result<bool>> HandleAsync(DeleteProduct request, CancellationToken ct = default)
    {
        var product = await _db.Products
            .FirstOrDefaultAsync(p => p.Id == request.Id, ct);

        if (product == null)
        {
            return Result.Failure<bool>(new KeyNotFoundException($"Product with ID '{request.Id}' was not found."));
        }

        // Soft delete: Remove from DbSet, SoftDeleteSaveChangesInterceptor converts this to Modified with DeletedAt & DeletedBy
        _db.Products.Remove(product);
        await _db.SaveChangesAsync(ct);

        return Result.Success(true);
    }
}
