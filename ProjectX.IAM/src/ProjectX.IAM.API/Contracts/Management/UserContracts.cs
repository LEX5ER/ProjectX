using ProjectX.IAM.Domain.Entities;

namespace ProjectX.IAM.API.Contracts.Management;

public sealed record UserRoleAssignmentResponse(
    Guid Id,
    Guid RoleId,
    string RoleName,
    RoleScope Scope,
    Guid? ProjectId,
    string? ProjectName,
    bool IsProtected,
    bool HasAllPermissions,
    string[] Permissions);

public sealed record UserResponse(
    Guid Id,
    bool IsProtected,
    string UserName,
    string Email,
    UserRoleAssignmentResponse[] Assignments,
    string[] Permissions);

public sealed record CreateUserRequest(
    string UserName,
    string Email,
    string Password,
    Guid[] GlobalRoleIds,
    Guid[] ProjectRoleIds);

public sealed record UpdateUserRequest(
    string UserName,
    string Email,
    string? Password,
    Guid[] GlobalRoleIds,
    Guid[] ProjectRoleIds);
