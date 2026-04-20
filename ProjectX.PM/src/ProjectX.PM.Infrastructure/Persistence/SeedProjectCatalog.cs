using ProjectX.PM.Domain.Entities;

namespace ProjectX.PM.Infrastructure.Persistence;

internal static class SeedProjectCatalog
{
    public static readonly SeedProjectDefinition[] All =
    [
        new(
            Guid.Parse("3D3AF99F-3A8F-4F18-8AA2-24DF6A16D101"),
            "IAM",
            "Project X - IAM",
            "Identity and access management application for Project X.",
            "Project X Platform",
            ProjectStatus.Active),
        new(
            Guid.Parse("B7E9F2C1-1648-4A60-83E4-06BF20B0D202"),
            "PM",
            "Project X - PM",
            "Project management application for Project X.",
            "Project X Platform",
            ProjectStatus.Active),
        new(
            Guid.Parse("16A650CC-0CF1-4702-8A08-1E1D14958303"),
            "POS",
            "Project X - POS",
            "Reserved project entry for the future Project X POS application.",
            "Project X Platform",
            ProjectStatus.Draft)
    ];
}

internal sealed record SeedProjectDefinition(
    Guid Id,
    string Code,
    string Name,
    string Description,
    string OwnerName,
    ProjectStatus Status);
