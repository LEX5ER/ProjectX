namespace ProjectX.IAM.Domain.Entities;

public sealed class UserRoleAssignment
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

    public Guid RoleId { get; set; }

    public Role Role { get; set; } = null!;

    public Guid? ProjectId { get; set; }
}
