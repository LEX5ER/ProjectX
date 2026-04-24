using Microsoft.EntityFrameworkCore;
using ProjectX.POS.Application.Abstractions;
using ProjectX.POS.Application.Auth;
using ProjectX.POS.Application.Sales;
using ProjectX.POS.Domain.Entities;

namespace ProjectX.POS.Application.Dashboard;

public sealed class DashboardService(
    IApplicationDbContext dbContext,
    IRequestContextAccessor requestContextAccessor,
    IIamAuthorizationContextService authorizationContextService) : IDashboardService
{
    public async Task<DashboardSummaryModel> GetSummaryAsync(CancellationToken cancellationToken)
    {
        var accessContext = await PosAuthorizationGuards.GetAuthorizationContextAsync(
            requestContextAccessor,
            authorizationContextService,
            cancellationToken);
        PosAuthorizationGuards.EnsureCanReadAny(accessContext, "dashboard data");

        var productsQuery = dbContext.Products
            .AsNoTracking()
            .AsQueryable();
        var customersQuery = dbContext.Customers
            .AsNoTracking()
            .AsQueryable();
        var salesQuery = dbContext.Sales
            .AsNoTracking()
            .AsQueryable();

        if (!accessContext.CanReadAllProducts && accessContext.ActiveProjectId.HasValue)
        {
            var activeProjectId = accessContext.ActiveProjectId.Value;
            productsQuery = productsQuery.Where(product => product.ProjectId == activeProjectId);
            customersQuery = customersQuery.Where(customer => customer.ProjectId == activeProjectId);
            salesQuery = salesQuery.Where(sale => sale.ProjectId == activeProjectId);
        }

        var today = DateTimeOffset.UtcNow.Date;
        var tomorrow = today.AddDays(1);
        var totalProducts = await productsQuery.CountAsync(cancellationToken);
        var totalCustomers = await customersQuery.CountAsync(cancellationToken);
        var lowStockProducts = await productsQuery.CountAsync(
            product => product.Status == ProductStatus.LowStock || product.Status == ProductStatus.OutOfStock,
            cancellationToken);
        var todaySalesAmount = await salesQuery
            .Where(sale => sale.CreatedAtUtc >= today && sale.CreatedAtUtc < tomorrow)
            .SumAsync(sale => (decimal?)sale.TotalAmount, cancellationToken) ?? 0m;
        var todaySalesCount = await salesQuery.CountAsync(
            sale => sale.CreatedAtUtc >= today && sale.CreatedAtUtc < tomorrow,
            cancellationToken);
        var todayRefundedAmount = await salesQuery
            .Where(sale => sale.RefundedAtUtc >= today && sale.RefundedAtUtc < tomorrow)
            .SumAsync(sale => (decimal?)sale.TotalAmount, cancellationToken) ?? 0m;
        var inventoryValue = await productsQuery
            .SumAsync(product => (decimal?)(product.UnitPrice * product.StockQuantity), cancellationToken) ?? 0m;
        var lowStockItems = await productsQuery
            .Where(product => product.Status == ProductStatus.LowStock || product.Status == ProductStatus.OutOfStock)
            .OrderBy(product => product.StockQuantity)
            .ThenBy(product => product.Name)
            .Take(6)
            .Select(product => new DashboardLowStockProductModel(
                product.Id,
                product.Code,
                product.Name,
                product.StockQuantity,
                product.Status))
            .ToListAsync(cancellationToken);
        var recentSaleEntities = await salesQuery
            .Include(sale => sale.LineItems)
            .OrderByDescending(sale => sale.CreatedAtUtc)
            .Take(5)
            .ToListAsync(cancellationToken);
        var recentSales = recentSaleEntities
            .Select(SalesService.MapSummary)
            .ToList();

        return new DashboardSummaryModel(
            totalProducts,
            totalCustomers,
            lowStockProducts,
            todaySalesCount,
            decimal.Round(todaySalesAmount, 2, MidpointRounding.AwayFromZero),
            decimal.Round(todayRefundedAmount, 2, MidpointRounding.AwayFromZero),
            decimal.Round(todaySalesAmount - todayRefundedAmount, 2, MidpointRounding.AwayFromZero),
            decimal.Round(inventoryValue, 2, MidpointRounding.AwayFromZero),
            lowStockItems,
            recentSales);
    }
}
