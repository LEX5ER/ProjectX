using ProjectX.IAM.Domain.Entities;

namespace ProjectX.IAM.Infrastructure.Auth;

public sealed record RoleSeedPreset(
    string Name,
    string Description,
    RoleScope Scope,
    bool HasAllPermissions,
    string[] PermissionNames);
