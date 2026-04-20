using ProjectX.POS.Domain.Entities;

namespace ProjectX.POS.Application.Products;

public interface IProductsService
{
    Task<PagedResult<ProductModel>> GetProductsAsync(int page, int pageSize, CancellationToken cancellationToken);

    Task<PagedResult<ProductModel>> GetCatalogAsync(int page, int pageSize, CancellationToken cancellationToken);

    Task<ProductModel?> GetProductByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<ProductModel> CreateProductAsync(
        string code,
        string name,
        string description,
        string category,
        decimal unitPrice,
        int stockQuantity,
        ProductStatus status,
        CancellationToken cancellationToken);

    Task<ProductModel?> UpdateProductAsync(
        Guid id,
        string code,
        string name,
        string description,
        string category,
        decimal unitPrice,
        int stockQuantity,
        ProductStatus status,
        CancellationToken cancellationToken);

    Task<bool> DeleteProductAsync(Guid id, CancellationToken cancellationToken);
}

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

public sealed record ProductModel(
    Guid Id,
    Guid ProjectId,
    string Code,
    string Name,
    string Description,
    string Category,
    decimal UnitPrice,
    int StockQuantity,
    ProductStatus Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
