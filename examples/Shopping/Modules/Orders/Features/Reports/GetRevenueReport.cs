using Kernel.Common;
using Kernel.Requests;
using Microsoft.EntityFrameworkCore;
using Orders.Contracts.Dtos;
using Orders.Entities;
using Orders.Persistence;

namespace Orders.Features.Reports;

public record GetRevenueReport(DateTime? From = null, DateTime? To = null, string? GroupBy = null) : IRequest<RevenueReportDto>;

public class GetRevenueReportHandler : IRequestHandler<GetRevenueReport, RevenueReportDto>
{
    private readonly OrdersDbContext _db;

    public GetRevenueReportHandler(OrdersDbContext db)
    {
        _db = db;
    }

    public async Task<Result<RevenueReportDto>> HandleAsync(GetRevenueReport request, CancellationToken ct = default)
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

        var orders = await query
            .OrderBy(o => o.CreatedAt)
            .ToListAsync(ct);

        var totalOrders = orders.Count;
        var totalRevenue = orders.Sum(o => o.TotalAmount);
        var averageOrderValue = totalOrders > 0 ? Math.Round(totalRevenue / totalOrders, 2) : 0m;

        var groupBy = (request.GroupBy ?? "day").Trim().ToLowerInvariant();
        List<RevenuePeriodBreakdownDto> breakdowns;

        switch (groupBy)
        {
            case "month":
                breakdowns = orders
                    .GroupBy(o => o.CreatedAt.ToString("yyyy-MM"))
                    .Select(g => new RevenuePeriodBreakdownDto(
                        g.Key,
                        g.Count(),
                        g.Sum(o => o.TotalAmount)))
                    .OrderBy(b => b.Period)
                    .ToList();
                break;

            case "total":
                breakdowns =
                [
                    new RevenuePeriodBreakdownDto(
                        "Total",
                        totalOrders,
                        totalRevenue)
                ];
                break;

            case "day":
            default:
                breakdowns = orders
                    .GroupBy(o => o.CreatedAt.ToString("yyyy-MM-dd"))
                    .Select(g => new RevenuePeriodBreakdownDto(
                        g.Key,
                        g.Count(),
                        g.Sum(o => o.TotalAmount)))
                    .OrderBy(b => b.Period)
                    .ToList();
                break;
        }

        var report = new RevenueReportDto(
            request.From,
            request.To,
            totalRevenue,
            totalOrders,
            averageOrderValue,
            breakdowns);

        return Result.Success(report);
    }
}
