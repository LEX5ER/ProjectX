using ProjectX.POS.Domain.Entities;

namespace ProjectX.POS.API.Contracts.Products;

public sealed record ProductResponse(
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

public sealed record CreateProductRequest(
    string Code,
    string Name,
    string Description,
    string Category,
    decimal UnitPrice,
    int StockQuantity,
    ProductStatus Status);

public sealed record UpdateProductRequest(
    string Code,
    string Name,
    string Description,
    string Category,
    decimal UnitPrice,
    int StockQuantity,
    ProductStatus Status);
