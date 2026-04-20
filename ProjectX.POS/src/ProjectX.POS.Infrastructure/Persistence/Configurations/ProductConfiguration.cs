using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectX.POS.Domain.Entities;

namespace ProjectX.POS.Infrastructure.Persistence.Configurations;

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products");

        builder.HasKey(product => product.Id);

        builder.Property(product => product.ProjectId)
            .IsRequired();

        builder.Property(product => product.Code)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(product => product.Name)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(product => product.Description)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(product => product.Category)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(product => product.UnitPrice)
            .HasPrecision(18, 2);

        builder.Property(product => product.StockQuantity)
            .IsRequired();

        builder.Property(product => product.Status)
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(product => product.CreatedAtUtc)
            .IsRequired();

        builder.Property(product => product.UpdatedAtUtc)
            .IsRequired();

        builder.HasIndex(product => new { product.ProjectId, product.Code })
            .IsUnique();

        builder.HasIndex(product => product.ProjectId);

        builder.HasIndex(product => product.Status);
    }
}
