using Microsoft.AspNetCore.Authorization;

namespace ProjectX.IAM.Infrastructure.Auth;

public sealed class PermissionAuthorizeAttribute : AuthorizeAttribute
{
    public const string PolicyPrefix = "Permission:";

    public PermissionAuthorizeAttribute(string permission)
    {
        Permission = permission;
    }

    public string? Permission
    {
        get => Policy is not null && Policy.StartsWith(PolicyPrefix, StringComparison.Ordinal)
            ? Policy[PolicyPrefix.Length..]
            : Policy;
        set => Policy = value is null ? null : $"{PolicyPrefix}{value}";
    }
}
