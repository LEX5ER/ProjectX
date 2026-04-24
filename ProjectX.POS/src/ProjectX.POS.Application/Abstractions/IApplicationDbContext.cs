using Microsoft.EntityFrameworkCore;
using ProjectX.POS.Domain.Entities;

namespace ProjectX.POS.Application.Abstractions;

public interface IApplicationDbContext
{
    DbSet<Customer> Customers { get; }

    DbSet<Product> Products { get; }

    DbSet<Sale> Sales { get; }

    DbSet<SaleLineItem> SaleLineItems { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
