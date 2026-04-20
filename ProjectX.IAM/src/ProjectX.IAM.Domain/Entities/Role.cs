namespace ProjectX.IAM.Domain.Entities;

public sealed class Role
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public RoleScope Scope { get; set; } = RoleScope.Project;

    public bool IsProtected { get; set; }

    public bool HasAllPermissions { get; set; }

    public Guid? ProjectId { get; set; }

    public string? ProjectName { get; set; }

    public ICollection<UserRoleAssignment> Assignments { get; } = [];

    public ICollection<Permission> Permissions { get; } = [];
}
