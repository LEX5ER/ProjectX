using Microsoft.EntityFrameworkCore;
using ProjectX.POS.Domain.Entities;

namespace ProjectX.POS.Application.Abstractions;

public interface IApplicationDbContext
{
    DbSet<Product> Products { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
