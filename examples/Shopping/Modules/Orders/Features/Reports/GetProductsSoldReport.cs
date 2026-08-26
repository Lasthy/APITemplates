using Kernel.Common;
using Kernel.Requests;
using Microsoft.EntityFrameworkCore;
using Orders.Contracts.Dtos;
using Orders.Entities;
using Orders.Persistence;

namespace Orders.Features.Reports;

public record GetProductsSoldReport(DateTime? From = null, DateTime? To = null) : IRequest<ProductsSoldReportDto>;

public class GetProductsSoldReportHandler : IRequestHandler<GetProductsSoldReport, ProductsSoldReportDto>
{
    private readonly OrdersDbContext _db;

    public GetProductsSoldReportHandler(OrdersDbContext db)
    {
        _db = db;
    }

    public async Task<Result<ProductsSoldReportDto>> HandleAsync(GetProductsSoldReport request, CancellationToken ct = default)
    {
        var query = _db.Orders
            .AsNoTracking()
            .Where(o => o.Status != OrderStatus.Cancelled);

        if (request.From.HasValue)
        {
            query = query.Where(o => o.CreatedAt >= request.From.Value);
        }

        if (request.To.HasValue)
        {
            query = query.Where(o => o.CreatedAt <= request.To.Value);
        }

        var orderItems = await query
            .SelectMany(o => o.Items)
            .ToListAsync(ct);

        var grouped = orderItems
            .GroupBy(i => new { i.ProductId, i.ProductName })
            .Select(g => new ProductSalesItemDto(
                g.Key.ProductId,
                g.Key.ProductName,
                g.Sum(i => i.Quantity),
                g.Sum(i => i.TotalPrice)))
            .OrderByDescending(p => p.TotalRevenue)
            .ToList();

        var totalUnitsSold = grouped.Sum(g => g.UnitsSold);
        var totalSalesAmount = grouped.Sum(g => g.TotalRevenue);
        var uniqueProductsCount = grouped.Count;

        var report = new ProductsSoldReportDto(
            request.From,
            request.To,
            totalUnitsSold,
            totalSalesAmount,
            uniqueProductsCount,
            grouped);

        return Result.Success(report);
    }
}
