using ProjectX.POS.Domain.Entities;

namespace ProjectX.POS.API.Contracts.Sales;

public sealed record CheckoutSaleLineRequest(
    Guid ProductId,
    int Quantity,
    decimal DiscountAmount);

public sealed record CheckoutSaleRequest(
    Guid? CustomerId,
    decimal CartDiscountAmount,
    decimal TaxRatePercentage,
    PaymentMethod PaymentMethod,
    decimal AmountReceived,
    string? Note,
    string? ReceiptEmail,
    IReadOnlyList<CheckoutSaleLineRequest> Lines);

public sealed record RefundSaleRequest(
    string Reason,
    bool Restock);
