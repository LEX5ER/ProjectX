using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectX.POS.Domain.Entities;

namespace ProjectX.POS.Infrastructure.Persistence.Configurations;

public sealed class SaleConfiguration : IEntityTypeConfiguration<Sale>
{
    public void Configure(EntityTypeBuilder<Sale> builder)
    {
        builder.ToTable("Sales");

        builder.HasKey(sale => sale.Id);

        builder.Property(sale => sale.ProjectId)
            .IsRequired();

        builder.Property(sale => sale.ReceiptNumber)
            .IsRequired()
            .HasMaxLength(32);

        builder.Property(sale => sale.CustomerName)
            .IsRequired()
            .HasMaxLength(201);

        builder.Property(sale => sale.CustomerEmail)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(sale => sale.CashierUserId)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(sale => sale.CashierUserName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(sale => sale.Status)
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(sale => sale.PaymentMethod)
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(sale => sale.SubtotalAmount)
            .HasPrecision(18, 2);

        builder.Property(sale => sale.LineDiscountAmount)
            .HasPrecision(18, 2);

        builder.Property(sale => sale.CartDiscountAmount)
            .HasPrecision(18, 2);

        builder.Property(sale => sale.TaxRatePercentage)
            .HasPrecision(9, 2);

        builder.Property(sale => sale.TaxAmount)
            .HasPrecision(18, 2);

        builder.Property(sale => sale.TotalAmount)
            .HasPrecision(18, 2);

        builder.Property(sale => sale.PaidAmount)
            .HasPrecision(18, 2);

        builder.Property(sale => sale.ChangeAmount)
            .HasPrecision(18, 2);

        builder.Property(sale => sale.Note)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(sale => sale.ReceiptEmail)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(sale => sale.RefundReason)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(sale => sale.RefundedByUserId)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(sale => sale.RefundedByUserName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(sale => sale.CreatedAtUtc)
            .IsRequired();

        builder.Property(sale => sale.UpdatedAtUtc)
            .IsRequired();

        builder.HasIndex(sale => new { sale.ProjectId, sale.CreatedAtUtc });

        builder.HasIndex(sale => sale.ReceiptNumber)
            .IsUnique();

        builder.HasIndex(sale => sale.Status);

        builder.HasOne(sale => sale.Customer)
            .WithMany(customer => customer.Sales)
            .HasForeignKey(sale => sale.CustomerId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
