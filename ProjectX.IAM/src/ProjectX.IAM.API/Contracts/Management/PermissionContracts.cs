using ProjectX.IAM.Domain.Entities;

namespace ProjectX.IAM.API.Contracts.Management;

public sealed record PermissionResponse(
    Guid Id,
    string Name,
    string Description,
    RoleScope Scope,
    Guid? ProjectId,
    string? ProjectName,
    int RoleCount);

public sealed record CreatePermissionRequest(string Name, string Description, RoleScope Scope);

public sealed record UpdatePermissionRequest(string Name, string Description, RoleScope Scope);
