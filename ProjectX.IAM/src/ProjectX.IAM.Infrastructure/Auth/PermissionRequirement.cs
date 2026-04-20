using Microsoft.AspNetCore.Authorization;

namespace ProjectX.IAM.Infrastructure.Auth;

public sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}
