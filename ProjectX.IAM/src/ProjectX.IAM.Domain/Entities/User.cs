namespace ProjectX.IAM.Domain.Entities;

public sealed class User
{
    public Guid Id { get; set; }

    public bool IsProtected { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public ICollection<UserRoleAssignment> RoleAssignments { get; } = [];

    public ICollection<RefreshToken> RefreshTokens { get; } = [];

    public ICollection<AuthenticationAuditEntry> AuthenticationAuditEntries { get; } = [];
}
