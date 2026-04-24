using ProjectX.POS.Application.Sales;
using ProjectX.POS.Domain.Entities;

namespace ProjectX.POS.Application.Dashboard;

public interface IDashboardService
{
    Task<DashboardSummaryModel> GetSummaryAsync(CancellationToken cancellationToken);
}

public sealed record DashboardLowStockProductModel(
    Guid Id,
    string Code,
    string Name,
    int StockQuantity,
    ProductStatus Status);

public sealed record DashboardSummaryModel(
    int TotalProducts,
    int TotalCustomers,
    int LowStockProducts,
    int TodaySalesCount,
    decimal TodaySalesAmount,
    decimal TodayRefundedAmount,
    decimal TodayNetSales,
    decimal InventoryValue,
    IReadOnlyList<DashboardLowStockProductModel> LowStockItems,
    IReadOnlyList<SaleSummaryModel> RecentSales);
