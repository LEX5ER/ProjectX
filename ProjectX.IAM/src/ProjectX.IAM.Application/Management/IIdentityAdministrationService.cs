using ProjectX.IAM.Domain.Entities;

namespace ProjectX.IAM.Application.Management;

public interface IIdentityAdministrationService
{
    Task<PagedResult<UserModel>> GetUsersAsync(int page, int pageSize, CancellationToken cancellationToken);

    Task<UserModel?> GetUserByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<UserModel> CreateUserAsync(
        string userName,
        string email,
        string password,
        Guid[]? globalRoleIds,
        Guid[]? projectRoleIds,
        CancellationToken cancellationToken);

    Task<UserModel?> UpdateUserAsync(
        Guid id,
        string userName,
        string email,
        string? password,
        Guid[]? globalRoleIds,
        Guid[]? projectRoleIds,
        CancellationToken cancellationToken);

    Task<bool> DeleteUserAsync(Guid id, CancellationToken cancellationToken);

    Task<PagedResult<RoleModel>> GetRolesAsync(int page, int pageSize, CancellationToken cancellationToken);

    Task<RoleModel?> GetRoleByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<RoleModel> CreateRoleAsync(
        string name,
        string description,
        RoleScope scope,
        CancellationToken cancellationToken);

    Task<RoleModel?> UpdateRoleAsync(
        Guid id,
        string name,
        string description,
        RoleScope scope,
        CancellationToken cancellationToken);

    Task<RoleModel?> UpdateRolePermissionsAsync(Guid id, Guid[]? permissionIds, CancellationToken cancellationToken);

    Task<bool> DeleteRoleAsync(Guid id, CancellationToken cancellationToken);

    Task<PagedResult<PermissionModel>> GetPermissionsAsync(int page, int pageSize, CancellationToken cancellationToken);

    Task<PermissionModel?> GetPermissionByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<PermissionModel> CreatePermissionAsync(
        string name,
        string description,
        RoleScope scope,
        CancellationToken cancellationToken);

    Task<PermissionModel?> UpdatePermissionAsync(
        Guid id,
        string name,
        string description,
        RoleScope scope,
        CancellationToken cancellationToken);

    Task<bool> DeletePermissionAsync(Guid id, CancellationToken cancellationToken);

    Task<PagedResult<ProjectModel>> GetProjectsAsync(int page, int pageSize, CancellationToken cancellationToken);

    Task<ProjectModel?> GetProjectByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<PagedResult<AuthenticationAuditModel>> GetAuthenticationAuditsAsync(int page, int pageSize, CancellationToken cancellationToken);
}

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

public sealed record PermissionModel(
    Guid Id,
    string Name,
    string Description,
    RoleScope Scope,
    Guid? ProjectId,
    string? ProjectName,
    int RoleCount);

public sealed record RoleModel(
    Guid Id,
    string Name,
    string Description,
    RoleScope Scope,
    Guid? ProjectId,
    string? ProjectName,
    bool IsProtected,
    bool HasAllPermissions,
    int AssignmentCount,
    PermissionModel[] Permissions);

public sealed record UserRoleAssignmentModel(
    Guid Id,
    Guid RoleId,
    string RoleName,
    RoleScope Scope,
    Guid? ProjectId,
    string? ProjectName,
    bool IsProtected,
    bool HasAllPermissions,
    string[] Permissions);

public sealed record UserModel(
    Guid Id,
    bool IsProtected,
    string UserName,
    string Email,
    UserRoleAssignmentModel[] Assignments,
    string[] Permissions);

public sealed record ProjectModel(
    Guid Id,
    string Name,
    string Description,
    int MemberCount);

public sealed record AuthenticationAuditModel(
    Guid Id,
    DateTimeOffset OccurredAtUtc,
    AuthenticationAuditAction Action,
    AuthenticationAuditOutcome Outcome,
    Guid? UserId,
    string? UserNameOrEmail,
    Guid? ProjectId,
    string? ProjectName,
    string? FailureReason,
    string? ClientApplication,
    string? IpAddress,
    string? UserAgent);
