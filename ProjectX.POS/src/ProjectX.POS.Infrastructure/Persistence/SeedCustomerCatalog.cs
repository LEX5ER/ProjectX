namespace ProjectX.POS.Infrastructure.Persistence;

internal static class SeedCustomerCatalog
{
    public static readonly SeedCustomerDefinition[] All =
    [
        new(
            Guid.Parse("AF5A2E15-9EB1-4D73-8A84-8A57FD4D5101"),
            SeedProductCatalog.PosProjectId,
            "Maria",
            "Santos",
            "maria.santos@example.com",
            "+63-917-555-0101",
            "Frequent walk-in customer for pantry and household essentials.",
            true,
            false),
        new(
            Guid.Parse("8D4AC2E7-DC8A-4E00-BEBE-82F4121D5102"),
            SeedProductCatalog.PosProjectId,
            "Luis",
            "Reyes",
            "luis.reyes@example.com",
            "+63-917-555-0102",
            "Business buyer who prefers emailed receipts.",
            false,
            true),
        new(
            Guid.Parse("8A3D4FE7-E6D1-4C10-845A-1A5A771D5103"),
            SeedProductCatalog.PosProjectId,
            "Anna",
            "Dela Cruz",
            "anna.delacruz@example.com",
            "+63-917-555-0103",
            "Regular customer interested in loyalty-style promotions.",
            true,
            false)
    ];
}

internal sealed record SeedCustomerDefinition(
    Guid Id,
    Guid ProjectId,
    string FirstName,
    string LastName,
    string Email,
    string Phone,
    string Notes,
    bool MarketingOptIn,
    bool TaxExempt);
