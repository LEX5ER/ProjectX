namespace ProjectX.POS.Domain.Entities;

public sealed class Customer
{
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;

    public bool MarketingOptIn { get; set; }

    public bool TaxExempt { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public ICollection<Sale> Sales { get; set; } = [];
}
