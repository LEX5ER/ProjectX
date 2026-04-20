using Microsoft.EntityFrameworkCore;
using ProjectX.PM.Domain.Entities;

namespace ProjectX.PM.Application.Abstractions;

public interface IApplicationDbContext
{
    DbSet<Project> Projects { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
