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
        await SeedCustomersAsync(dbContext);
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

    private static async Task SeedCustomersAsync(ApplicationDbContext dbContext)
    {
        var now = DateTimeOffset.UtcNow;
        var seededCustomers = SeedCustomerCatalog.All;
        var customerIds = seededCustomers
            .Select(customer => customer.Id)
            .ToArray();
        var existingCustomers = await dbContext.Customers
            .AsTracking()
            .Where(customer => customerIds.Contains(customer.Id))
            .ToDictionaryAsync(customer => customer.Id);
        var hasChanges = false;

        foreach (var seededCustomer in seededCustomers)
        {
            if (!existingCustomers.TryGetValue(seededCustomer.Id, out var customer))
            {
                dbContext.Customers.Add(new Customer
                {
                    Id = seededCustomer.Id,
                    ProjectId = seededCustomer.ProjectId,
                    FirstName = seededCustomer.FirstName,
                    LastName = seededCustomer.LastName,
                    Email = seededCustomer.Email,
                    Phone = seededCustomer.Phone,
                    Notes = seededCustomer.Notes,
                    MarketingOptIn = seededCustomer.MarketingOptIn,
                    TaxExempt = seededCustomer.TaxExempt,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                });

                hasChanges = true;
                continue;
            }

            var customerChanged = false;

            if (customer.ProjectId != seededCustomer.ProjectId)
            {
                customer.ProjectId = seededCustomer.ProjectId;
                customerChanged = true;
            }

            if (!string.Equals(customer.FirstName, seededCustomer.FirstName, StringComparison.Ordinal))
            {
                customer.FirstName = seededCustomer.FirstName;
                customerChanged = true;
            }

            if (!string.Equals(customer.LastName, seededCustomer.LastName, StringComparison.Ordinal))
            {
                customer.LastName = seededCustomer.LastName;
                customerChanged = true;
            }

            if (!string.Equals(customer.Email, seededCustomer.Email, StringComparison.Ordinal))
            {
                customer.Email = seededCustomer.Email;
                customerChanged = true;
            }

            if (!string.Equals(customer.Phone, seededCustomer.Phone, StringComparison.Ordinal))
            {
                customer.Phone = seededCustomer.Phone;
                customerChanged = true;
            }

            if (!string.Equals(customer.Notes, seededCustomer.Notes, StringComparison.Ordinal))
            {
                customer.Notes = seededCustomer.Notes;
                customerChanged = true;
            }

            if (customer.MarketingOptIn != seededCustomer.MarketingOptIn)
            {
                customer.MarketingOptIn = seededCustomer.MarketingOptIn;
                customerChanged = true;
            }

            if (customer.TaxExempt != seededCustomer.TaxExempt)
            {
                customer.TaxExempt = seededCustomer.TaxExempt;
                customerChanged = true;
            }

            if (customerChanged)
            {
                customer.UpdatedAtUtc = now;
                hasChanges = true;
            }
        }

        if (hasChanges)
        {
            await dbContext.SaveChangesAsync();
        }
    }
}
