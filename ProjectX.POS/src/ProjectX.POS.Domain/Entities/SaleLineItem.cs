namespace ProjectX.POS.Domain.Entities;

public sealed class SaleLineItem
{
    public Guid Id { get; set; }

    public Guid SaleId { get; set; }

    public Guid ProductId { get; set; }

    public string ProductCode { get; set; } = string.Empty;

    public string ProductName { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal LineSubtotalAmount { get; set; }

    public decimal LineTotalAmount { get; set; }

    public Sale Sale { get; set; } = null!;
}
