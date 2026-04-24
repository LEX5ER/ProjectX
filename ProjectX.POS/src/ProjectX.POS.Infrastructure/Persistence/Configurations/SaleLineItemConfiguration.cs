using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectX.POS.Domain.Entities;

namespace ProjectX.POS.Infrastructure.Persistence.Configurations;

public sealed class SaleLineItemConfiguration : IEntityTypeConfiguration<SaleLineItem>
{
    public void Configure(EntityTypeBuilder<SaleLineItem> builder)
    {
        builder.ToTable("SaleLineItems");

        builder.HasKey(lineItem => lineItem.Id);

        builder.Property(lineItem => lineItem.ProductId)
            .IsRequired();

        builder.Property(lineItem => lineItem.ProductCode)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(lineItem => lineItem.ProductName)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(lineItem => lineItem.Category)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(lineItem => lineItem.Quantity)
            .IsRequired();

        builder.Property(lineItem => lineItem.UnitPrice)
            .HasPrecision(18, 2);

        builder.Property(lineItem => lineItem.DiscountAmount)
            .HasPrecision(18, 2);

        builder.Property(lineItem => lineItem.LineSubtotalAmount)
            .HasPrecision(18, 2);

        builder.Property(lineItem => lineItem.LineTotalAmount)
            .HasPrecision(18, 2);

        builder.HasIndex(lineItem => lineItem.SaleId);

        builder.HasOne(lineItem => lineItem.Sale)
            .WithMany(sale => sale.LineItems)
            .HasForeignKey(lineItem => lineItem.SaleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
