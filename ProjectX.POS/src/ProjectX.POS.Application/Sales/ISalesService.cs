using ProjectX.POS.Application.Products;
using ProjectX.POS.Domain.Entities;

namespace ProjectX.POS.Application.Sales;

public interface ISalesService
{
    Task<PagedResult<SaleSummaryModel>> GetSalesAsync(string? search, int page, int pageSize, CancellationToken cancellationToken);

    Task<SaleDetailModel?> GetSaleByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<SaleDetailModel> CheckoutAsync(CheckoutSaleInput input, CancellationToken cancellationToken);

    Task<SaleDetailModel?> RefundSaleAsync(Guid id, RefundSaleInput input, CancellationToken cancellationToken);
}

public sealed record CheckoutSaleInput(
    Guid? CustomerId,
    decimal CartDiscountAmount,
    decimal TaxRatePercentage,
    PaymentMethod PaymentMethod,
    decimal AmountReceived,
    string? Note,
    string? ReceiptEmail,
    IReadOnlyList<CheckoutSaleLineInput> Lines);

public sealed record CheckoutSaleLineInput(
    Guid ProductId,
    int Quantity,
    decimal DiscountAmount);

public sealed record RefundSaleInput(
    string Reason,
    bool Restock);

public sealed record SaleSummaryModel(
    Guid Id,
    Guid ProjectId,
    string ReceiptNumber,
    Guid? CustomerId,
    string CustomerName,
    string CashierUserName,
    SaleStatus Status,
    PaymentMethod PaymentMethod,
    int TotalItems,
    decimal TotalAmount,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? RefundedAtUtc,
    bool RestockedOnRefund);

public sealed record SaleLineModel(
    Guid Id,
    Guid ProductId,
    string ProductCode,
    string ProductName,
    string Category,
    int Quantity,
    decimal UnitPrice,
    decimal DiscountAmount,
    decimal LineSubtotalAmount,
    decimal LineTotalAmount);

public sealed record SaleDetailModel(
    Guid Id,
    Guid ProjectId,
    string ReceiptNumber,
    Guid? CustomerId,
    string CustomerName,
    string CustomerEmail,
    string CashierUserName,
    SaleStatus Status,
    PaymentMethod PaymentMethod,
    decimal SubtotalAmount,
    decimal LineDiscountAmount,
    decimal CartDiscountAmount,
    decimal TaxRatePercentage,
    decimal TaxAmount,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal ChangeAmount,
    string Note,
    string ReceiptEmail,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? RefundedAtUtc,
    string RefundReason,
    string RefundedByUserName,
    bool RestockedOnRefund,
    IReadOnlyList<SaleLineModel> LineItems);
