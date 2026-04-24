using ProjectX.POS.Domain.Entities;

namespace ProjectX.POS.Application.Products;

internal static class ProductInventoryRules
{
    public const int LowStockThreshold = 15;

    public static ProductStatus NormalizeRequestedStatus(ProductStatus requestedStatus, int stockQuantity)
    {
        return requestedStatus switch
        {
            ProductStatus.Draft => ProductStatus.Draft,
            ProductStatus.Archived => ProductStatus.Archived,
            _ => GetInventoryStatus(stockQuantity)
        };
    }

    public static void ApplyInventoryStatus(Product product)
    {
        if (product.Status is ProductStatus.Draft or ProductStatus.Archived)
        {
            return;
        }

        product.Status = GetInventoryStatus(product.StockQuantity);
    }

    public static bool CanSell(Product product)
    {
        return product.Status is not ProductStatus.Draft
            and not ProductStatus.Archived
            && product.StockQuantity > 0;
    }

    private static ProductStatus GetInventoryStatus(int stockQuantity)
    {
        if (stockQuantity <= 0)
        {
            return ProductStatus.OutOfStock;
        }

        return stockQuantity <= LowStockThreshold
            ? ProductStatus.LowStock
            : ProductStatus.Active;
    }
}
