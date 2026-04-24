using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectX.POS.Domain.Entities;

namespace ProjectX.POS.Infrastructure.Persistence.Configurations;

public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers");

        builder.HasKey(customer => customer.Id);

        builder.Property(customer => customer.ProjectId)
            .IsRequired();

        builder.Property(customer => customer.FirstName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(customer => customer.LastName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(customer => customer.Email)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(customer => customer.Phone)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(customer => customer.Notes)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(customer => customer.CreatedAtUtc)
            .IsRequired();

        builder.Property(customer => customer.UpdatedAtUtc)
            .IsRequired();

        builder.HasIndex(customer => customer.ProjectId);

        builder.HasIndex(customer => new { customer.ProjectId, customer.LastName, customer.FirstName });

        builder.HasIndex(customer => new { customer.ProjectId, customer.Email });

        builder.HasIndex(customer => new { customer.ProjectId, customer.Phone });
    }
}
