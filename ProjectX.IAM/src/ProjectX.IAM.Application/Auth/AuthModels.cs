namespace ProjectX.IAM.Application.Auth;

public sealed record AuthSession(AuthUserProfile User, AuthTokenBundle Tokens);

public sealed record AuthTokenBundle(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAtUtc,
    string IdentityToken,
    DateTimeOffset IdentityTokenExpiresAtUtc,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAtUtc);

public sealed record AuthProjectProfile(
    Guid Id,
    string Name);

public sealed record AuthUserProfile(
    Guid Id,
    string UserName,
    string Email,
    bool HasGlobalFullAccess,
    IReadOnlyList<string> GlobalRoles,
    IReadOnlyList<string> GlobalPermissions,
    IReadOnlyList<AuthProjectProfile> Projects,
    Guid? ActiveProjectId,
    string? ActiveProjectName,
    IReadOnlyList<string> ActiveProjectPermissions,
    IReadOnlyList<string> Roles,
    bool HasAllPermissions,
    IReadOnlyList<string> Permissions);
