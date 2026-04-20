using ProjectX.POS.Domain.Entities;

namespace ProjectX.POS.Infrastructure.Persistence;

internal static class SeedProductCatalog
{
    public static readonly Guid PosProjectId = Guid.Parse("16A650CC-0CF1-4702-8A08-1E1D14958303");

    public static readonly SeedProductDefinition[] All =
    [
        new(
            Guid.Parse("52EAA4B3-57EA-4D96-86C4-FDCC6DD2A101"),
            PosProjectId,
            "BEV-COLA-330",
            "Cola 330ml",
            "Chilled bottled cola for fast-moving checkout lanes.",
            "Beverages",
            35.00m,
            84,
            ProductStatus.Active),
        new(
            Guid.Parse("B352A26B-46A6-4D31-8BA1-4E2AA6CFB202"),
            PosProjectId,
            "SNK-CHIPS-BBQ",
            "BBQ Potato Chips",
            "Single-serve barbecue chips for counter and impulse displays.",
            "Snacks",
            52.50m,
            12,
            ProductStatus.LowStock),
        new(
            Guid.Parse("4EC3FE32-51A2-4858-BD41-F084E88B3203"),
            PosProjectId,
            "ACC-USBC-2M",
            "USB-C Cable 2m",
            "Accessory item for device counters and service desks.",
            "Accessories",
            249.00m,
            0,
            ProductStatus.OutOfStock),
        new(
            Guid.Parse("A38DCE04-0F91-4728-8F7D-293F4CC4E404"),
            PosProjectId,
            "HYG-HSAN-100",
            "Hand Sanitizer 100ml",
            "Front-of-store hygiene essential for quick retail replenishment.",
            "Essentials",
            79.00m,
            31,
            ProductStatus.Active)
    ];
}

internal sealed record SeedProductDefinition(
    Guid Id,
    Guid ProjectId,
    string Code,
    string Name,
    string Description,
    string Category,
    decimal UnitPrice,
    int StockQuantity,
    ProductStatus Status);
