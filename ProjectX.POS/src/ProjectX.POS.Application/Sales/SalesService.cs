using Microsoft.EntityFrameworkCore;
using ProjectX.POS.Application.Abstractions;
using ProjectX.POS.Application.Auth;
using ProjectX.POS.Application.Customers;
using ProjectX.POS.Application.Products;
using ProjectX.POS.Domain.Entities;

namespace ProjectX.POS.Application.Sales;

public sealed class SalesService(
    IApplicationDbContext dbContext,
    IRequestContextAccessor requestContextAccessor,
    IIamAuthorizationContextService authorizationContextService) : ISalesService
{
    private const int MaxPageSize = 100;

    public async Task<PagedResult<SaleSummaryModel>> GetSalesAsync(
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var accessContext = await PosAuthorizationGuards.GetAuthorizationContextAsync(
            requestContextAccessor,
            authorizationContextService,
            cancellationToken);
        PosAuthorizationGuards.EnsureCanReadAny(accessContext, "sales");

        var query = dbContext.Sales
            .AsNoTracking()
            .Include(sale => sale.LineItems)
            .AsQueryable();

        if (!accessContext.CanReadAllProducts && accessContext.ActiveProjectId.HasValue)
        {
            query = query.Where(sale => sale.ProjectId == accessContext.ActiveProjectId.Value);
        }

        query = ApplySearch(query, search);

        return await CreatePagedResultAsync(query, page, pageSize, cancellationToken);
    }

    public async Task<SaleDetailModel?> GetSaleByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var sale = await dbContext.Sales
            .AsNoTracking()
            .Include(currentSale => currentSale.LineItems)
            .FirstOrDefaultAsync(currentSale => currentSale.Id == id, cancellationToken);

        if (sale is null)
        {
            return null;
        }

        var accessContext = await PosAuthorizationGuards.GetAuthorizationContextAsync(
            requestContextAccessor,
            authorizationContextService,
            cancellationToken);
        PosAuthorizationGuards.EnsureCanReadProject(accessContext, sale.ProjectId, "sales");

        return MapDetail(sale);
    }

    public async Task<SaleDetailModel> CheckoutAsync(CheckoutSaleInput input, CancellationToken cancellationToken)
    {
        ValidateCheckoutInput(input);

        var accessContext = await PosAuthorizationGuards.GetAuthorizationContextAsync(
            requestContextAccessor,
            authorizationContextService,
            cancellationToken);
        var projectId = PosAuthorizationGuards.RequireActiveProjectForManagement(accessContext, "sales");
        var normalizedLines = input.Lines
            .GroupBy(line => line.ProductId)
            .Select(group => new CheckoutSaleLineInput(
                group.Key,
                group.Sum(line => line.Quantity),
                decimal.Round(group.Sum(line => line.DiscountAmount), 2, MidpointRounding.AwayFromZero)))
            .ToArray();
        var requestedProductIds = normalizedLines
            .Select(line => line.ProductId)
            .Distinct()
            .ToArray();
        var products = await dbContext.Products
            .Where(product => product.ProjectId == projectId && requestedProductIds.Contains(product.Id))
            .ToDictionaryAsync(product => product.Id, cancellationToken);

        if (products.Count != requestedProductIds.Length)
        {
            throw new ApplicationConflictException("One or more cart items are no longer available in the active IAM project.");
        }

        var now = DateTimeOffset.UtcNow;
        var saleLineItems = new List<SaleLineItem>(normalizedLines.Length);
        decimal subtotalAmount = 0;
        decimal lineDiscountAmount = 0;

        foreach (var line in normalizedLines)
        {
            var product = products[line.ProductId];

            if (!ProductInventoryRules.CanSell(product))
            {
                throw new ApplicationConflictException($"Product \"{product.Name}\" is not currently available for checkout.");
            }

            if (product.StockQuantity < line.Quantity)
            {
                throw new ApplicationConflictException($"Product \"{product.Name}\" does not have enough stock to fulfill the requested quantity.");
            }

            var currentLineSubtotal = decimal.Round(product.UnitPrice * line.Quantity, 2, MidpointRounding.AwayFromZero);
            var currentLineDiscount = decimal.Round(line.DiscountAmount, 2, MidpointRounding.AwayFromZero);

            if (currentLineDiscount > currentLineSubtotal)
            {
                throw new ApplicationValidationException(new Dictionary<string, string[]>
                {
                    ["lines"] = [$"Discount for product \"{product.Name}\" cannot exceed the line subtotal."]
                });
            }

            var currentLineTotal = decimal.Round(currentLineSubtotal - currentLineDiscount, 2, MidpointRounding.AwayFromZero);

            saleLineItems.Add(new SaleLineItem
            {
                Id = Guid.NewGuid(),
                ProductId = product.Id,
                ProductCode = product.Code,
                ProductName = product.Name,
                Category = product.Category,
                Quantity = line.Quantity,
                UnitPrice = product.UnitPrice,
                DiscountAmount = currentLineDiscount,
                LineSubtotalAmount = currentLineSubtotal,
                LineTotalAmount = currentLineTotal
            });

            subtotalAmount += currentLineSubtotal;
            lineDiscountAmount += currentLineDiscount;
            product.StockQuantity -= line.Quantity;
            product.UpdatedAtUtc = now;
            ProductInventoryRules.ApplyInventoryStatus(product);
        }

        subtotalAmount = decimal.Round(subtotalAmount, 2, MidpointRounding.AwayFromZero);
        lineDiscountAmount = decimal.Round(lineDiscountAmount, 2, MidpointRounding.AwayFromZero);
        var customer = await ResolveCustomerAsync(projectId, input.CustomerId, cancellationToken);
        var appliedTaxRatePercentage = customer?.TaxExempt == true
            ? 0m
            : decimal.Round(input.TaxRatePercentage, 2, MidpointRounding.AwayFromZero);

        var maximumCartDiscount = Math.Max(0, subtotalAmount - lineDiscountAmount);
        var cartDiscountAmount = decimal.Round(
            Math.Min(Math.Max(0, input.CartDiscountAmount), maximumCartDiscount),
            2,
            MidpointRounding.AwayFromZero);
        var taxableBaseAmount = decimal.Round(subtotalAmount - lineDiscountAmount - cartDiscountAmount, 2, MidpointRounding.AwayFromZero);
        var taxAmount = decimal.Round(taxableBaseAmount * (appliedTaxRatePercentage / 100m), 2, MidpointRounding.AwayFromZero);
        var totalAmount = decimal.Round(taxableBaseAmount + taxAmount, 2, MidpointRounding.AwayFromZero);
        var paidAmount = input.PaymentMethod == PaymentMethod.Cash
            ? decimal.Round(input.AmountReceived, 2, MidpointRounding.AwayFromZero)
            : totalAmount;
        var changeAmount = input.PaymentMethod == PaymentMethod.Cash
            ? decimal.Round(Math.Max(0, paidAmount - totalAmount), 2, MidpointRounding.AwayFromZero)
            : 0m;

        if (input.PaymentMethod == PaymentMethod.Cash && paidAmount < totalAmount)
        {
            throw new ApplicationValidationException(new Dictionary<string, string[]>
            {
                ["amountReceived"] = ["Cash received must cover the sale total."]
            });
        }

        var sale = new Sale
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            ReceiptNumber = BuildReceiptNumber(now),
            CustomerId = customer?.Id,
            CustomerName = customer is null ? string.Empty : BuildCustomerDisplayName(customer),
            CustomerEmail = customer?.Email ?? string.Empty,
            CashierUserId = accessContext.UserId.ToString("D"),
            CashierUserName = accessContext.UserName,
            Status = SaleStatus.Completed,
            PaymentMethod = input.PaymentMethod,
            SubtotalAmount = subtotalAmount,
            LineDiscountAmount = lineDiscountAmount,
            CartDiscountAmount = cartDiscountAmount,
            TaxRatePercentage = appliedTaxRatePercentage,
            TaxAmount = taxAmount,
            TotalAmount = totalAmount,
            PaidAmount = paidAmount,
            ChangeAmount = changeAmount,
            Note = NormalizeOptional(input.Note),
            ReceiptEmail = NormalizeOptional(input.ReceiptEmail),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            LineItems = saleLineItems
        };

        dbContext.Sales.Add(sale);
        await dbContext.SaveChangesAsync(cancellationToken);

        return MapDetail(sale);
    }

    public async Task<SaleDetailModel?> RefundSaleAsync(Guid id, RefundSaleInput input, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(input.Reason))
        {
            throw new ApplicationValidationException(new Dictionary<string, string[]>
            {
                ["reason"] = ["Refund reason is required."]
            });
        }

        var sale = await dbContext.Sales
            .Include(currentSale => currentSale.LineItems)
            .FirstOrDefaultAsync(currentSale => currentSale.Id == id, cancellationToken);

        if (sale is null)
        {
            return null;
        }

        var accessContext = await PosAuthorizationGuards.GetAuthorizationContextAsync(
            requestContextAccessor,
            authorizationContextService,
            cancellationToken);
        PosAuthorizationGuards.EnsureCanManageProject(accessContext, sale.ProjectId, "sales");

        if (sale.Status == SaleStatus.Refunded)
        {
            throw new ApplicationConflictException("This sale has already been refunded.");
        }

        var now = DateTimeOffset.UtcNow;

        if (input.Restock)
        {
            var productIds = sale.LineItems
                .Select(lineItem => lineItem.ProductId)
                .Distinct()
                .ToArray();
            var products = await dbContext.Products
                .Where(product => product.ProjectId == sale.ProjectId && productIds.Contains(product.Id))
                .ToDictionaryAsync(product => product.Id, cancellationToken);

            foreach (var lineItem in sale.LineItems)
            {
                if (!products.TryGetValue(lineItem.ProductId, out var product))
                {
                    continue;
                }

                product.StockQuantity += lineItem.Quantity;
                product.UpdatedAtUtc = now;

                if (product.Status is not ProductStatus.Draft and not ProductStatus.Archived)
                {
                    ProductInventoryRules.ApplyInventoryStatus(product);
                }
            }
        }

        sale.Status = SaleStatus.Refunded;
        sale.RefundReason = input.Reason.Trim();
        sale.RefundedAtUtc = now;
        sale.RefundedByUserId = accessContext.UserId.ToString("D");
        sale.RefundedByUserName = accessContext.UserName;
        sale.RestockedOnRefund = input.Restock;
        sale.UpdatedAtUtc = now;

        await dbContext.SaveChangesAsync(cancellationToken);

        return MapDetail(sale);
    }

    private static IQueryable<Sale> ApplySearch(IQueryable<Sale> query, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return query;
        }

        var normalizedSearch = search.Trim();

        return query.Where(sale =>
            sale.ReceiptNumber.Contains(normalizedSearch)
            || sale.CustomerName.Contains(normalizedSearch)
            || sale.CashierUserName.Contains(normalizedSearch)
            || sale.Note.Contains(normalizedSearch));
    }

    private async Task<Customer?> ResolveCustomerAsync(Guid projectId, Guid? customerId, CancellationToken cancellationToken)
    {
        if (!customerId.HasValue)
        {
            return null;
        }

        var customer = await dbContext.Customers
            .FirstOrDefaultAsync(currentCustomer => currentCustomer.Id == customerId.Value, cancellationToken);

        if (customer is null || customer.ProjectId != projectId)
        {
            throw new ApplicationConflictException("The selected customer is not available in the active IAM project.");
        }

        return customer;
    }

    private static void ValidateCheckoutInput(CheckoutSaleInput input)
    {
        var errors = new Dictionary<string, string[]>();

        if (input.Lines is null || input.Lines.Count == 0)
        {
            errors["lines"] = ["At least one cart line is required to complete a sale."];
        }

        if (input.TaxRatePercentage < 0)
        {
            errors["taxRatePercentage"] = ["Tax rate cannot be negative."];
        }

        if (input.CartDiscountAmount < 0)
        {
            errors["cartDiscountAmount"] = ["Cart discount cannot be negative."];
        }

        if (input.Lines is not null)
        {
            foreach (var line in input.Lines)
            {
                if (line.Quantity <= 0)
                {
                    errors["lines"] = ["Each cart line must have a quantity greater than zero."];
                    break;
                }

                if (line.DiscountAmount < 0)
                {
                    errors["lines"] = ["Line discounts cannot be negative."];
                    break;
                }
            }
        }

        if (errors.Count > 0)
        {
            throw new ApplicationValidationException(errors);
        }
    }

    internal static SaleSummaryModel MapSummary(Sale sale)
    {
        return new SaleSummaryModel(
            sale.Id,
            sale.ProjectId,
            sale.ReceiptNumber,
            sale.CustomerId,
            sale.CustomerName,
            sale.CashierUserName,
            sale.Status,
            sale.PaymentMethod,
            sale.LineItems.Sum(lineItem => lineItem.Quantity),
            sale.TotalAmount,
            sale.CreatedAtUtc,
            sale.RefundedAtUtc,
            sale.RestockedOnRefund);
    }

    internal static SaleDetailModel MapDetail(Sale sale)
    {
        var orderedLineItems = sale.LineItems
            .OrderBy(lineItem => lineItem.ProductName)
            .ThenBy(lineItem => lineItem.ProductCode)
            .Select(lineItem => new SaleLineModel(
                lineItem.Id,
                lineItem.ProductId,
                lineItem.ProductCode,
                lineItem.ProductName,
                lineItem.Category,
                lineItem.Quantity,
                lineItem.UnitPrice,
                lineItem.DiscountAmount,
                lineItem.LineSubtotalAmount,
                lineItem.LineTotalAmount))
            .ToArray();

        return new SaleDetailModel(
            sale.Id,
            sale.ProjectId,
            sale.ReceiptNumber,
            sale.CustomerId,
            sale.CustomerName,
            sale.CustomerEmail,
            sale.CashierUserName,
            sale.Status,
            sale.PaymentMethod,
            sale.SubtotalAmount,
            sale.LineDiscountAmount,
            sale.CartDiscountAmount,
            sale.TaxRatePercentage,
            sale.TaxAmount,
            sale.TotalAmount,
            sale.PaidAmount,
            sale.ChangeAmount,
            sale.Note,
            sale.ReceiptEmail,
            sale.CreatedAtUtc,
            sale.UpdatedAtUtc,
            sale.RefundedAtUtc,
            sale.RefundReason,
            sale.RefundedByUserName,
            sale.RestockedOnRefund,
            orderedLineItems);
    }

    private static string BuildReceiptNumber(DateTimeOffset timestamp)
    {
        var value = $"POS-{timestamp:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}";
        return value[..Math.Min(value.Length, 32)];
    }

    private static string BuildCustomerDisplayName(Customer customer)
    {
        return string.Join(" ", new[] { customer.FirstName.Trim(), customer.LastName.Trim() }.Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static string NormalizeOptional(string? value)
    {
        return value?.Trim() ?? string.Empty;
    }

    private static async Task<PagedResult<SaleSummaryModel>> CreatePagedResultAsync(
        IQueryable<Sale> query,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var normalizedPage = Math.Max(1, page);
        var normalizedPageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        var skip = (normalizedPage - 1) * normalizedPageSize;
        var totalCount = await query.CountAsync(cancellationToken);
        var sales = await query
            .OrderByDescending(sale => sale.CreatedAtUtc)
            .ThenByDescending(sale => sale.UpdatedAtUtc)
            .Skip(skip)
            .Take(normalizedPageSize)
            .ToListAsync(cancellationToken);
        var items = sales
            .Select(MapSummary)
            .ToList();
        var totalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)normalizedPageSize);

        return new PagedResult<SaleSummaryModel>(items, normalizedPage, normalizedPageSize, totalCount, totalPages);
    }
}
