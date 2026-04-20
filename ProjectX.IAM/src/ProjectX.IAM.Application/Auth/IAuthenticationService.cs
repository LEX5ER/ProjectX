namespace ProjectX.IAM.Application.Auth;

public interface IAuthenticationService
{
    Task<AuthSession?> LoginAsync(string userNameOrEmail, string password, CancellationToken cancellationToken);

    Task<AuthSession?> RefreshAsync(string refreshToken, Guid? activeProjectId, CancellationToken cancellationToken);

    Task LogoutAsync(string refreshToken, CancellationToken cancellationToken);

    Task<AuthUserProfile?> GetUserAsync(
        Guid userId,
        string? authorizationHeader,
        Guid? activeProjectId,
        CancellationToken cancellationToken);
}
