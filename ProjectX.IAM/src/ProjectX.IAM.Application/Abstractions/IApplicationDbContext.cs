using Microsoft.EntityFrameworkCore;
using ProjectX.IAM.Domain.Entities;

namespace ProjectX.IAM.Application.Abstractions;

public interface IApplicationDbContext
{
    DbSet<User> Users { get; }

    DbSet<Role> Roles { get; }

    DbSet<Permission> Permissions { get; }

    DbSet<UserRoleAssignment> UserRoleAssignments { get; }

    DbSet<RefreshToken> RefreshTokens { get; }

    DbSet<AuthenticationAuditEntry> AuthenticationAuditEntries { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
