namespace ProjectX.POS.Domain.Entities;

public sealed class Sale
{
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    public string ReceiptNumber { get; set; } = string.Empty;

    public Guid? CustomerId { get; set; }

    public string CustomerName { get; set; } = string.Empty;

    public string CustomerEmail { get; set; } = string.Empty;

    public string CashierUserId { get; set; } = string.Empty;

    public string CashierUserName { get; set; } = string.Empty;

    public SaleStatus Status { get; set; } = SaleStatus.Completed;

    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;

    public decimal SubtotalAmount { get; set; }

    public decimal LineDiscountAmount { get; set; }

    public decimal CartDiscountAmount { get; set; }

    public decimal TaxRatePercentage { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal TotalAmount { get; set; }

    public decimal PaidAmount { get; set; }

    public decimal ChangeAmount { get; set; }

    public string Note { get; set; } = string.Empty;

    public string ReceiptEmail { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public DateTimeOffset? RefundedAtUtc { get; set; }

    public string RefundReason { get; set; } = string.Empty;

    public string RefundedByUserId { get; set; } = string.Empty;

    public string RefundedByUserName { get; set; } = string.Empty;

    public bool RestockedOnRefund { get; set; }

    public Customer? Customer { get; set; }

    public ICollection<SaleLineItem> LineItems { get; set; } = [];
}
