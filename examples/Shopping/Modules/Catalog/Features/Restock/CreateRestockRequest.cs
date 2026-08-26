using Catalog.Contracts.Dtos;
using Catalog.Entities;
using Catalog.Persistence;
using Kernel.Common;
using Kernel.Context;
using Kernel.Requests;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Features.Restock;

public record CreateRestockRequest(
    DefaultId ProductId,
    int Quantity,
    string Reason) : IRequest<RestockRequestDto>;

public class CreateRestockRequestHandler : IRequestHandler<CreateRestockRequest, RestockRequestDto>
{
    private readonly CatalogDbContext _db;
    private readonly IScopeContext _scopeContext;

    public CreateRestockRequestHandler(CatalogDbContext db, IScopeContext scopeContext)
    {
        _db = db;
        _scopeContext = scopeContext;
    }

    public async Task<Result<RestockRequestDto>> HandleAsync(CreateRestockRequest request, CancellationToken ct = default)
    {
        if (request.Quantity <= 0)
        {
            return Result.Failure<RestockRequestDto>(new ArgumentException("Requested quantity must be greater than zero."));
        }

        var product = await _db.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.ProductId, ct);

        if (product == null)
        {
            return Result.Failure<RestockRequestDto>(new KeyNotFoundException($"Product with ID '{request.ProductId}' was not found."));
        }

        var requestedBy = _scopeContext.UserId ?? DefaultId.Empty;

        var restock = new RestockRequest
        {
            ProductId = product.Id,
            ProductName = product.Name,
            RequestedQuantity = request.Quantity,
            Reason = request.Reason?.Trim() ?? string.Empty,
            Status = RestockStatus.Pending,
            RequestedBy = requestedBy
        };

        _db.RestockRequests.Add(restock);
        await _db.SaveChangesAsync(ct);

        var dto = new RestockRequestDto(
            restock.Id,
            restock.ProductId,
            restock.ProductName,
            restock.RequestedQuantity,
            restock.Reason,
            restock.Status,
            restock.RequestedBy,
            restock.CreatedAt,
            restock.FulfilledBy,
            restock.FulfilledAt);

        return Result.Success(dto);
    }
}
