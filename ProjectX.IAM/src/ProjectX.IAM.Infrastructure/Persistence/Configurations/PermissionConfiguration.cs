using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectX.IAM.Domain.Entities;

namespace ProjectX.IAM.Infrastructure.Persistence.Configurations;

internal sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("Permissions");

        builder.HasKey(permission => permission.Id);

        builder.Property(permission => permission.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(permission => permission.Description)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(permission => permission.ProjectName)
            .HasMaxLength(100);

        builder.HasIndex(permission => permission.Name)
            .IsUnique()
            .HasFilter("[ProjectId] IS NULL");

        builder.HasIndex(permission => new { permission.ProjectId, permission.Name })
            .IsUnique()
            .HasFilter("[ProjectId] IS NOT NULL");
    }
}
