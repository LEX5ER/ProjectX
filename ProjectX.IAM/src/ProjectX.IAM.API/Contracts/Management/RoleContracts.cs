using ProjectX.IAM.Domain.Entities;

namespace ProjectX.IAM.API.Contracts.Management;

public sealed record RoleResponse(
    Guid Id,
    string Name,
    string Description,
    RoleScope Scope,
    Guid? ProjectId,
    string? ProjectName,
    bool IsProtected,
    bool HasAllPermissions,
    int AssignmentCount,
    PermissionResponse[] Permissions);

public sealed record CreateRoleRequest(string Name, string Description, RoleScope Scope);

public sealed record UpdateRoleRequest(string Name, string Description, RoleScope Scope);

public sealed record UpdateRolePermissionsRequest(Guid[] PermissionIds);
