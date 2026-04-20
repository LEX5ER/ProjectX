namespace ProjectX.IAM.Domain.Entities;

public sealed class Permission
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public Guid? ProjectId { get; set; }

    public string? ProjectName { get; set; }

    public ICollection<Role> Roles { get; } = [];
}
