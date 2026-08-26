namespace Orders.Contracts.Dtos;

/// <summary>
/// Sales metric per individual product within a specified time period.
/// </summary>
public record ProductSalesItemDto(
    DefaultId ProductId,
    string ProductName,
    int UnitsSold,
    decimal TotalRevenue);

/// <summary>
/// Report summarizing all products sold within a given time period.
/// </summary>
public record ProductsSoldReportDto(
    DateTime? PeriodFrom,
    DateTime? PeriodTo,
    int TotalUnitsSold,
    decimal TotalSalesAmount,
    int UniqueProductsCount,
    IReadOnlyList<ProductSalesItemDto> Items);

/// <summary>
/// Periodic breakdown (e.g. daily, monthly) of revenue and order volume.
/// </summary>
public record RevenuePeriodBreakdownDto(
    string Period,
    int OrderCount,
    decimal Revenue);

/// <summary>
/// Financial revenue report for a specified time period.
/// </summary>
public record RevenueReportDto(
    DateTime? PeriodFrom,
    DateTime? PeriodTo,
    decimal TotalRevenue,
    int TotalOrders,
    decimal AverageOrderValue,
    IReadOnlyList<RevenuePeriodBreakdownDto> Breakdowns);
