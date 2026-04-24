using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectX.POS.API.Contracts.Sales;
using ProjectX.POS.Application.Products;
using ProjectX.POS.Application.Sales;

namespace ProjectX.POS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class SalesController(ISalesService salesService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<SaleSummaryModel>>> GetAll(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        return Ok(await salesService.GetSalesAsync(search, page, pageSize, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SaleDetailModel>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var sale = await salesService.GetSaleByIdAsync(id, cancellationToken);
        return sale is null ? NotFound() : Ok(sale);
    }

    [HttpPost("checkout")]
    public async Task<ActionResult<SaleDetailModel>> Checkout(
        [FromBody] CheckoutSaleRequest request,
        CancellationToken cancellationToken)
    {
        var sale = await salesService.CheckoutAsync(
            new CheckoutSaleInput(
                request.CustomerId,
                request.CartDiscountAmount,
                request.TaxRatePercentage,
                request.PaymentMethod,
                request.AmountReceived,
                request.Note,
                request.ReceiptEmail,
                request.Lines
                    .Select(line => new CheckoutSaleLineInput(line.ProductId, line.Quantity, line.DiscountAmount))
                    .ToArray()),
            cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = sale.Id }, sale);
    }

    [HttpPost("{id:guid}/refund")]
    public async Task<ActionResult<SaleDetailModel>> Refund(
        Guid id,
        [FromBody] RefundSaleRequest request,
        CancellationToken cancellationToken)
    {
        var sale = await salesService.RefundSaleAsync(
            id,
            new RefundSaleInput(request.Reason, request.Restock),
            cancellationToken);

        return sale is null ? NotFound() : Ok(sale);
    }
}
