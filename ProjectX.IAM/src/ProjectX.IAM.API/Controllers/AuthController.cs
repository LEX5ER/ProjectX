using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectX.IAM.API.Contracts.Auth;
using ProjectX.IAM.Application.Auth;
using ProjectX.IAM.Infrastructure.Auth;

namespace ProjectX.IAM.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class AuthController(IAuthenticationService authenticationService) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<TokenResponse>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var session = await authenticationService.LoginAsync(request.UserNameOrEmail, request.Password, cancellationToken);

        if (session is null)
        {
            return Unauthorized(new ProblemDetails
            {
                Title = "Invalid credentials",
                Detail = "The username/email or password is incorrect.",
                Status = StatusCodes.Status401Unauthorized
            });
        }

        return Ok(Map(session));
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<ActionResult<TokenResponse>> Refresh([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var session = await authenticationService.RefreshAsync(request.RefreshToken, TryGetProjectId(), cancellationToken);

        if (session is null)
        {
            return Unauthorized(new ProblemDetails
            {
                Title = "Invalid refresh token",
                Detail = "The refresh token is invalid, expired, or revoked.",
                Status = StatusCodes.Status401Unauthorized
            });
        }

        return Ok(Map(session));
    }

    [AllowAnonymous]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest request, CancellationToken cancellationToken)
    {
        await authenticationService.LogoutAsync(request.RefreshToken, cancellationToken);
        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<AuthUserResponse>> Me(CancellationToken cancellationToken)
    {
        var subject = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

        if (!Guid.TryParse(subject, out var userId))
        {
            return Unauthorized();
        }

        var authorizationHeader = HttpContext.Request.Headers.Authorization.ToString();
        var activeProjectId = TryGetProjectId();
        var user = await authenticationService.GetUserAsync(userId, authorizationHeader, activeProjectId, cancellationToken);

        return user is null ? Unauthorized() : Ok(Map(user));
    }

    private static TokenResponse Map(AuthSession session)
    {
        return new TokenResponse(
            session.Tokens.AccessToken,
            session.Tokens.AccessTokenExpiresAtUtc,
            session.Tokens.IdentityToken,
            session.Tokens.IdentityTokenExpiresAtUtc,
            session.Tokens.RefreshToken,
            session.Tokens.RefreshTokenExpiresAtUtc,
            Map(session.User));
    }

    private static AuthUserResponse Map(AuthUserProfile user)
    {
        return new AuthUserResponse(
            user.Id,
            user.UserName,
            user.Email,
            user.HasGlobalFullAccess,
            user.Projects
                .Select(project => new AuthProjectResponse(
                    project.Id,
                    project.Name))
                .ToArray(),
            user.ActiveProjectId,
            user.ActiveProjectName,
            user.GlobalRoles.ToArray(),
            user.GlobalPermissions.ToArray(),
            user.ActiveProjectPermissions.ToArray(),
            user.Roles.ToArray(),
            user.HasAllPermissions,
            user.Permissions.ToArray());
    }

    private Guid? TryGetProjectId()
    {
        var rawValue = HttpContext.Request.Headers[ProjectContextHeaderNames.ProjectId].ToString();
        return Guid.TryParse(rawValue, out var projectId) ? projectId : null;
    }
}
