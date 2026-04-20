using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectX.IAM.Domain.Entities;

namespace ProjectX.IAM.Infrastructure.Persistence.Configurations;

internal sealed class UserRoleAssignmentConfiguration : IEntityTypeConfiguration<UserRoleAssignment>
{
    public void Configure(EntityTypeBuilder<UserRoleAssignment> builder)
    {
        builder.ToTable("UserRoleAssignments");

        builder.HasKey(assignment => assignment.Id);

        builder.HasIndex(assignment => new { assignment.UserId, assignment.RoleId, assignment.ProjectId })
            .IsUnique()
            .HasFilter("[ProjectId] IS NOT NULL");

        builder.HasIndex(assignment => new { assignment.UserId, assignment.RoleId })
            .IsUnique()
            .HasFilter("[ProjectId] IS NULL");

        builder.HasIndex(assignment => new { assignment.ProjectId, assignment.UserId });
    }
}
