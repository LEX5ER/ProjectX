using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProjectX.POS.Domain.Entities;

namespace ProjectX.POS.Infrastructure.Persistence;

public static class ApplicationInitializationExtensions
{
    public static async Task InitializePersistenceAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await dbContext.Database.MigrateAsync();
        await SeedProductsAsync(dbContext);
    }

    private static async Task SeedProductsAsync(ApplicationDbContext dbContext)
    {
        var now = DateTimeOffset.UtcNow;
        var seededProducts = SeedProductCatalog.All;
        var existingKeys = seededProducts
            .Select(product => CreateLookupKey(product.ProjectId, product.Code))
            .ToArray();
        var existingProducts = await dbContext.Products
            .AsTracking()
            .ToListAsync();
        var existingByKey = existingProducts
            .Where(product => existingKeys.Contains(CreateLookupKey(product.ProjectId, product.Code), StringComparer.OrdinalIgnoreCase))
            .ToDictionary(product => CreateLookupKey(product.ProjectId, product.Code), StringComparer.OrdinalIgnoreCase);
        var hasChanges = false;

        foreach (var seededProduct in seededProducts)
        {
            var lookupKey = CreateLookupKey(seededProduct.ProjectId, seededProduct.Code);

            if (!existingByKey.TryGetValue(lookupKey, out var product))
            {
                dbContext.Products.Add(new Product
                {
                    Id = seededProduct.Id,
                    ProjectId = seededProduct.ProjectId,
                    Code = seededProduct.Code,
                    Name = seededProduct.Name,
                    Description = seededProduct.Description,
                    Category = seededProduct.Category,
                    UnitPrice = seededProduct.UnitPrice,
                    StockQuantity = seededProduct.StockQuantity,
                    Status = seededProduct.Status,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                });

                hasChanges = true;
                continue;
            }

            var productChanged = false;

            if (product.ProjectId != seededProduct.ProjectId)
            {
                product.ProjectId = seededProduct.ProjectId;
                productChanged = true;
            }

            if (!string.Equals(product.Name, seededProduct.Name, StringComparison.Ordinal))
            {
                product.Name = seededProduct.Name;
                productChanged = true;
            }

            if (!string.Equals(product.Description, seededProduct.Description, StringComparison.Ordinal))
            {
                product.Description = seededProduct.Description;
                productChanged = true;
            }

            if (!string.Equals(product.Category, seededProduct.Category, StringComparison.Ordinal))
            {
                product.Category = seededProduct.Category;
                productChanged = true;
            }

            if (product.UnitPrice != seededProduct.UnitPrice)
            {
                product.UnitPrice = seededProduct.UnitPrice;
                productChanged = true;
            }

            if (product.StockQuantity != seededProduct.StockQuantity)
            {
                product.StockQuantity = seededProduct.StockQuantity;
                productChanged = true;
            }

            if (product.Status != seededProduct.Status)
            {
                product.Status = seededProduct.Status;
                productChanged = true;
            }

            if (!string.Equals(product.Code, seededProduct.Code, StringComparison.Ordinal))
            {
                product.Code = seededProduct.Code;
                productChanged = true;
            }

            if (productChanged)
            {
                product.UpdatedAtUtc = now;
                hasChanges = true;
            }
        }

        if (hasChanges)
        {
            await dbContext.SaveChangesAsync();
        }
    }

    private static string CreateLookupKey(Guid projectId, string code)
    {
        return $"{projectId:N}:{code}";
    }
}
