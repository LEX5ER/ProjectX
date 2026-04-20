using Microsoft.EntityFrameworkCore;
using ProjectX.POS.Application.Abstractions;
using ProjectX.POS.Application.Auth;
using ProjectX.POS.Domain.Entities;

namespace ProjectX.POS.Application.Products;

public sealed class ProductsService(
    IApplicationDbContext dbContext,
    IRequestContextAccessor requestContextAccessor,
    IIamAuthorizationContextService authorizationContextService) : IProductsService
{
    private const int MaxPageSize = 100;

    public async Task<PagedResult<ProductModel>> GetProductsAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var accessContext = await GetAuthorizationContextAsync(cancellationToken);
        EnsureCanReadAnyProduct(accessContext);

        var query = dbContext.Products
            .AsNoTracking()
            .AsQueryable();

        if (!accessContext.CanReadAllProducts && accessContext.ActiveProjectId.HasValue)
        {
            query = query.Where(product => product.ProjectId == accessContext.ActiveProjectId.Value);
        }

        return await CreatePagedResultAsync(query, page, pageSize, cancellationToken);
    }

    public Task<PagedResult<ProductModel>> GetCatalogAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        return GetProductsAsync(page, pageSize, cancellationToken);
    }

    public async Task<ProductModel?> GetProductByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var product = await dbContext.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(currentProduct => currentProduct.Id == id, cancellationToken);

        if (product is null)
        {
            return null;
        }

        var accessContext = await GetAuthorizationContextAsync(cancellationToken);
        EnsureCanReadProduct(accessContext, product.ProjectId);

        return Map(product);
    }

    public async Task<ProductModel> CreateProductAsync(
        string code,
        string name,
        string description,
        string category,
        decimal unitPrice,
        int stockQuantity,
        ProductStatus status,
        CancellationToken cancellationToken)
    {
        var accessContext = await GetAuthorizationContextAsync(cancellationToken);
        EnsureCanCreateProduct(accessContext);
        ValidateProduct(code, name, description, category, unitPrice, stockQuantity);

        var projectId = accessContext.ActiveProjectId
            ?? throw new ApplicationForbiddenException("Select the POS IAM project before creating products.");

        var normalizedCode = code.Trim().ToUpperInvariant();
        var duplicateExists = await dbContext.Products
            .AnyAsync(product => product.ProjectId == projectId && product.Code == normalizedCode, cancellationToken);

        if (duplicateExists)
        {
            throw new ApplicationConflictException("A product with the same code already exists in the active IAM project.");
        }

        var now = DateTimeOffset.UtcNow;
        var product = new Product
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Code = normalizedCode,
            Name = name.Trim(),
            Description = description.Trim(),
            Category = category.Trim(),
            UnitPrice = decimal.Round(unitPrice, 2, MidpointRounding.AwayFromZero),
            StockQuantity = stockQuantity,
            Status = status,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Map(product);
    }

    public async Task<ProductModel?> UpdateProductAsync(
        Guid id,
        string code,
        string name,
        string description,
        string category,
        decimal unitPrice,
        int stockQuantity,
        ProductStatus status,
        CancellationToken cancellationToken)
    {
        ValidateProduct(code, name, description, category, unitPrice, stockQuantity);

        var product = await dbContext.Products
            .FirstOrDefaultAsync(currentProduct => currentProduct.Id == id, cancellationToken);

        if (product is null)
        {
            return null;
        }

        var accessContext = await GetAuthorizationContextAsync(cancellationToken);
        EnsureCanManageProduct(accessContext, product.ProjectId);

        var normalizedCode = code.Trim().ToUpperInvariant();
        var duplicateExists = await dbContext.Products
            .AnyAsync(
                currentProduct =>
                    currentProduct.Id != id
                    && currentProduct.ProjectId == product.ProjectId
                    && currentProduct.Code == normalizedCode,
                cancellationToken);

        if (duplicateExists)
        {
            throw new ApplicationConflictException("A product with the same code already exists in the active IAM project.");
        }

        product.Code = normalizedCode;
        product.Name = name.Trim();
        product.Description = description.Trim();
        product.Category = category.Trim();
        product.UnitPrice = decimal.Round(unitPrice, 2, MidpointRounding.AwayFromZero);
        product.StockQuantity = stockQuantity;
        product.Status = status;
        product.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        return Map(product);
    }

    public async Task<bool> DeleteProductAsync(Guid id, CancellationToken cancellationToken)
    {
        var product = await dbContext.Products
            .FirstOrDefaultAsync(currentProduct => currentProduct.Id == id, cancellationToken);

        if (product is null)
        {
            return false;
        }

        var accessContext = await GetAuthorizationContextAsync(cancellationToken);
        EnsureCanManageProduct(accessContext, product.ProjectId);

        dbContext.Products.Remove(product);
        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    private async Task<PosAuthorizationContext> GetAuthorizationContextAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await authorizationContextService.GetCurrentAsync(requestContextAccessor.GetCurrent(), cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException)
        {
            throw new ApplicationServiceUnavailableException(
                "IAM authorization unavailable",
                "ProjectX.POS could not validate the current IAM project.");
        }
    }

    private static void EnsureCanReadAnyProduct(PosAuthorizationContext accessContext)
    {
        if (accessContext.CanReadAnyProduct)
        {
            return;
        }

        throw new ApplicationForbiddenException(DescribeScopedAccessFailure(accessContext, "view"));
    }

    private static void EnsureCanReadProduct(PosAuthorizationContext accessContext, Guid projectId)
    {
        if (accessContext.CanReadProduct(projectId))
        {
            return;
        }

        throw new ApplicationForbiddenException(DescribeProductFailure(accessContext, projectId, "view"));
    }

    private static void EnsureCanCreateProduct(PosAuthorizationContext accessContext)
    {
        if (accessContext.CanCreateProduct)
        {
            return;
        }

        throw new ApplicationForbiddenException(DescribeScopedAccessFailure(accessContext, "create"));
    }

    private static void EnsureCanManageProduct(PosAuthorizationContext accessContext, Guid projectId)
    {
        if (accessContext.CanManageProduct(projectId))
        {
            return;
        }

        throw new ApplicationForbiddenException(DescribeProductFailure(accessContext, projectId, "manage"));
    }

    private static void ValidateProduct(
        string code,
        string name,
        string description,
        string category,
        decimal unitPrice,
        int stockQuantity)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(code))
        {
            errors["code"] = ["Code is required."];
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            errors["name"] = ["Name is required."];
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            errors["description"] = ["Description is required."];
        }

        if (string.IsNullOrWhiteSpace(category))
        {
            errors["category"] = ["Category is required."];
        }

        if (unitPrice < 0)
        {
            errors["unitPrice"] = ["Unit price cannot be negative."];
        }

        if (stockQuantity < 0)
        {
            errors["stockQuantity"] = ["Stock quantity cannot be negative."];
        }

        if (errors.Count > 0)
        {
            throw new ApplicationValidationException(errors);
        }
    }

    private static string DescribeScopedAccessFailure(PosAuthorizationContext accessContext, string action)
    {
        if (!accessContext.ActiveProjectId.HasValue)
        {
            return $"Select the POS IAM project before attempting to {action} products.";
        }

        return $"This session does not have permission to {action} products for the active IAM project.";
    }

    private static string DescribeProductFailure(PosAuthorizationContext accessContext, Guid projectId, string action)
    {
        if (accessContext.ActiveProjectId == projectId)
        {
            return $"This session does not have permission to {action} products for the active IAM project.";
        }

        if (accessContext.ActiveProjectId.HasValue)
        {
            return $"This session can only {action} products linked to the active IAM project.";
        }

        return DescribeScopedAccessFailure(accessContext, action);
    }

    private static ProductModel Map(Product product)
    {
        return new ProductModel(
            product.Id,
            product.ProjectId,
            product.Code,
            product.Name,
            product.Description,
            product.Category,
            product.UnitPrice,
            product.StockQuantity,
            product.Status,
            product.CreatedAtUtc,
            product.UpdatedAtUtc);
    }

    private static async Task<PagedResult<ProductModel>> CreatePagedResultAsync(
        IQueryable<Product> query,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var normalizedPage = Math.Max(1, page);
        var normalizedPageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        var skip = (normalizedPage - 1) * normalizedPageSize;
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(product => product.UpdatedAtUtc)
            .ThenBy(product => product.Name)
            .Skip(skip)
            .Take(normalizedPageSize)
            .Select(product => Map(product))
            .ToListAsync(cancellationToken);
        var totalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)normalizedPageSize);

        return new PagedResult<ProductModel>(items, normalizedPage, normalizedPageSize, totalCount, totalPages);
    }
}
