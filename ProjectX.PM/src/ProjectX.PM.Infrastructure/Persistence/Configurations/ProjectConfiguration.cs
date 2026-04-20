using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectX.PM.Domain.Entities;

namespace ProjectX.PM.Infrastructure.Persistence.Configurations;

public sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("Projects");

        builder.HasKey(project => project.Id);

        builder.Property(project => project.Code)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(project => project.Name)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(project => project.Description)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(project => project.OwnerName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(project => project.Status)
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(project => project.CreatedAtUtc)
            .IsRequired();

        builder.Property(project => project.UpdatedAtUtc)
            .IsRequired();

        builder.HasIndex(project => project.Code)
            .IsUnique();

        builder.HasIndex(project => project.Status);
    }
}
