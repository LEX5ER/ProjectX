using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectX.IAM.Domain.Entities;

namespace ProjectX.IAM.Infrastructure.Persistence.Configurations;

internal sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Roles");

        builder.HasKey(role => role.Id);

        builder.Property(role => role.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(role => role.Description)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(role => role.ProjectName)
            .HasMaxLength(100);

        builder.Property(role => role.Scope)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(role => role.IsProtected)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(role => role.HasAllPermissions)
            .HasDefaultValue(false)
            .IsRequired();

        builder.HasIndex(role => new { role.Scope, role.Name })
            .IsUnique()
            .HasFilter("[ProjectId] IS NULL");

        builder.HasIndex(role => new { role.Scope, role.ProjectId, role.Name })
            .IsUnique()
            .HasFilter("[ProjectId] IS NOT NULL");

        builder.HasMany(role => role.Assignments)
            .WithOne(assignment => assignment.Role)
            .HasForeignKey(assignment => assignment.RoleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(role => role.Permissions)
            .WithMany(permission => permission.Roles)
            .UsingEntity<Dictionary<string, object>>(
                "RolePermissions",
                right => right
                    .HasOne<Permission>()
                    .WithMany()
                    .HasForeignKey("PermissionId")
                    .OnDelete(DeleteBehavior.Cascade),
                left => left
                    .HasOne<Role>()
                    .WithMany()
                    .HasForeignKey("RoleId")
                    .OnDelete(DeleteBehavior.Cascade),
                join =>
                {
                    join.ToTable("RolePermissions");
                    join.HasKey("RoleId", "PermissionId");
                    join.HasIndex("PermissionId");
                });
    }
}
