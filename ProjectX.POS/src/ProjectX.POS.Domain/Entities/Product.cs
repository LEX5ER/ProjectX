namespace ProjectX.POS.Domain.Entities;

public sealed class Product
{
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public decimal UnitPrice { get; set; }

    public int StockQuantity { get; set; }

    public ProductStatus Status { get; set; } = ProductStatus.Draft;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
