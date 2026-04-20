using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectX.IAM.Domain.Entities;

namespace ProjectX.IAM.Infrastructure.Persistence.Configurations;

internal sealed class AuthenticationAuditEntryConfiguration : IEntityTypeConfiguration<AuthenticationAuditEntry>
{
    public void Configure(EntityTypeBuilder<AuthenticationAuditEntry> builder)
    {
        builder.ToTable("AuthenticationAuditEntries");

        builder.HasKey(entry => entry.Id);

        builder.Property(entry => entry.UserNameOrEmail)
            .HasMaxLength(256);

        builder.Property(entry => entry.Action)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(entry => entry.Outcome)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(entry => entry.FailureReason)
            .HasMaxLength(256);

        builder.Property(entry => entry.ClientApplication)
            .HasMaxLength(64);

        builder.Property(entry => entry.IpAddress)
            .HasMaxLength(64);

        builder.Property(entry => entry.UserAgent)
            .HasMaxLength(512);

        builder.Property(entry => entry.OccurredAtUtc)
            .IsRequired();

        builder.HasIndex(entry => entry.OccurredAtUtc);

        builder.HasIndex(entry => new { entry.UserId, entry.OccurredAtUtc });

        builder.HasOne(entry => entry.User)
            .WithMany(user => user.AuthenticationAuditEntries)
            .HasForeignKey(entry => entry.UserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
