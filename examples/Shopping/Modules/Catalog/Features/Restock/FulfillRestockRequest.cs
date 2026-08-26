using Catalog.Contracts.Dtos;
using Catalog.Contracts.Events;
using Catalog.Entities;
using Catalog.Persistence;
using Kernel.Common;
using Kernel.Context;
using Kernel.Events;
using Kernel.Requests;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Features.Restock;

public record FulfillRestockRequest(DefaultId RequestId) : IRequest<RestockRequestDto>;

public class FulfillRestockRequestHandler : IRequestHandler<FulfillRestockRequest, RestockRequestDto>
{
    private readonly CatalogDbContext _db;
    private readonly IScopeContext _scopeContext;
    private readonly IEventBus _eventBus;

    public FulfillRestockRequestHandler(
        CatalogDbContext db,
        IScopeContext scopeContext,
        IEventBus eventBus)
    {
        _db = db;
        _scopeContext = scopeContext;
        _eventBus = eventBus;
    }

    public async Task<Result<RestockRequestDto>> HandleAsync(FulfillRestockRequest request, CancellationToken ct = default)
    {
        var restock = await _db.RestockRequests
            .FirstOrDefaultAsync(r => r.Id == request.RequestId, ct);

        if (restock == null)
        {
            return Result.Failure<RestockRequestDto>(new KeyNotFoundException($"Restock request with ID '{request.RequestId}' was not found."));
        }

        if (restock.Status == RestockStatus.Fulfilled)
        {
            return Result.Failure<RestockRequestDto>(new InvalidOperationException("Restock request has already been fulfilled."));
        }

        var product = await _db.Products
            .FirstOrDefaultAsync(p => p.Id == restock.ProductId, ct);

        if (product == null)
        {
            return Result.Failure<RestockRequestDto>(new KeyNotFoundException($"Product with ID '{restock.ProductId}' was not found in catalog."));
        }

        // Add quantity to product stock
        product.StockQuantity += restock.RequestedQuantity;

        // Update restock request
        restock.Status = RestockStatus.Fulfilled;
        restock.FulfilledBy = _scopeContext.UserId;
        restock.FulfilledAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        await _eventBus.PublishAsync(new ProductRestockedEvent(
            product.Id,
            restock.RequestedQuantity,
            product.StockQuantity), ct);

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
