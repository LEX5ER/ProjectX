namespace ProjectX.IAM.API.Contracts.Auth;

public sealed record LoginRequest(string UserNameOrEmail, string Password);

public sealed record RefreshTokenRequest(string RefreshToken);

public sealed record LogoutRequest(string RefreshToken);

public sealed record AuthProjectResponse(
    Guid Id,
    string Name);

public sealed record AuthUserResponse(
    Guid Id,
    string UserName,
    string Email,
    bool HasGlobalFullAccess,
    AuthProjectResponse[] Projects,
    Guid? ActiveProjectId,
    string? ActiveProjectName,
    string[] GlobalRoles,
    string[] GlobalPermissions,
    string[] ActiveProjectPermissions,
    string[] Roles,
    bool HasAllPermissions,
    string[] Permissions);

public sealed record TokenResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAtUtc,
    string IdentityToken,
    DateTimeOffset IdentityTokenExpiresAtUtc,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAtUtc,
    AuthUserResponse User);
