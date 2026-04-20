using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ProjectX.IAM.Application.Abstractions;
using ProjectX.IAM.Application.Authorization;
using ProjectX.IAM.Application.Auth;
using ProjectX.IAM.Domain.Entities;
using ProjectX.IAM.Infrastructure.Persistence;

namespace ProjectX.IAM.Infrastructure.Auth;

public sealed class AuthenticationService : IAuthenticationService
{
    private const string InvalidCredentialsReason = "Invalid credentials.";
    private const string MissingCredentialsReason = "Missing credentials.";
    private const string InvalidRefreshTokenReason = "Invalid refresh token.";
    private const string MissingRefreshTokenReason = "Missing refresh token.";
    private const string RevokedRefreshTokenReason = "Refresh token already revoked.";

    private readonly ApplicationDbContext dbContext;
    private readonly IPasswordHasher<User> passwordHasher;
    private readonly IRequestContextAccessor requestContextAccessor;
    private readonly JwtOptions jwtOptions;
    private readonly JwtSecurityTokenHandler tokenHandler = new();

    public AuthenticationService(
        ApplicationDbContext dbContext,
        IPasswordHasher<User> passwordHasher,
        IRequestContextAccessor requestContextAccessor,
        IOptions<JwtOptions> jwtOptions)
    {
        this.dbContext = dbContext;
        this.passwordHasher = passwordHasher;
        this.requestContextAccessor = requestContextAccessor;
        this.jwtOptions = jwtOptions.Value;
    }

    public async Task<AuthSession?> LoginAsync(string userNameOrEmail, string password, CancellationToken cancellationToken)
    {
        var requestContext = requestContextAccessor.GetCurrent();
        var normalizedIdentifier = NormalizeIdentifier(userNameOrEmail);

        if (string.IsNullOrWhiteSpace(userNameOrEmail) || string.IsNullOrWhiteSpace(password))
        {
            await WriteAuditAsync(
                userId: null,
                userNameOrEmail: normalizedIdentifier,
                action: AuthenticationAuditAction.Login,
                outcome: AuthenticationAuditOutcome.Failed,
                failureReason: MissingCredentialsReason,
                requestContext,
                cancellationToken);
            return null;
        }

        var user = await LoadUserQuery()
            .FirstOrDefaultAsync(
                currentUser => currentUser.UserName == userNameOrEmail || currentUser.Email == userNameOrEmail,
                cancellationToken);

        if (user is null)
        {
            await WriteAuditAsync(
                userId: null,
                userNameOrEmail: normalizedIdentifier,
                action: AuthenticationAuditAction.Login,
                outcome: AuthenticationAuditOutcome.Failed,
                failureReason: InvalidCredentialsReason,
                requestContext,
                cancellationToken);
            return null;
        }

        var passwordVerification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);

        if (passwordVerification == PasswordVerificationResult.Failed)
        {
            await WriteAuditAsync(
                user.Id,
                normalizedIdentifier,
                AuthenticationAuditAction.Login,
                AuthenticationAuditOutcome.Failed,
                InvalidCredentialsReason,
                requestContext,
                cancellationToken);
            return null;
        }

        if (passwordVerification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = passwordHasher.HashPassword(user, password);
        }

        var session = await IssueSessionAsync(user, null, null, cancellationToken);

        dbContext.AuthenticationAuditEntries.Add(CreateAuditEntry(
            user.Id,
            normalizedIdentifier ?? user.UserName,
            session.User.ActiveProjectId,
            AuthenticationAuditAction.Login,
            AuthenticationAuditOutcome.Succeeded,
            failureReason: null,
            requestContext,
            DateTimeOffset.UtcNow));
        await dbContext.SaveChangesAsync(cancellationToken);

        return session;
    }

    public async Task<AuthSession?> RefreshAsync(string refreshToken, Guid? activeProjectId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        var refreshTokenHash = HashToken(refreshToken);

        var storedRefreshToken = await dbContext.RefreshTokens
            .Include(token => token.User)
                .ThenInclude(user => user.RoleAssignments)
                    .ThenInclude(assignment => assignment.Role)
                        .ThenInclude(role => role.Permissions)
            .FirstOrDefaultAsync(token => token.TokenHash == refreshTokenHash, cancellationToken);

        if (storedRefreshToken is null || storedRefreshToken.RevokedAtUtc is not null || storedRefreshToken.ExpiresAtUtc <= now)
        {
            return null;
        }

        storedRefreshToken.RevokedAtUtc = now;

        return await IssueSessionAsync(storedRefreshToken.User, storedRefreshToken, activeProjectId, cancellationToken);
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var requestContext = requestContextAccessor.GetCurrent();

        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            await WriteAuditAsync(
                userId: requestContext.UserId,
                userNameOrEmail: null,
                action: AuthenticationAuditAction.Logout,
                outcome: AuthenticationAuditOutcome.Failed,
                failureReason: MissingRefreshTokenReason,
                requestContext,
                cancellationToken);
            return;
        }

        var refreshTokenHash = HashToken(refreshToken);
        var storedRefreshToken = await dbContext.RefreshTokens
            .Include(token => token.User)
            .FirstOrDefaultAsync(token => token.TokenHash == refreshTokenHash, cancellationToken);

        if (storedRefreshToken is null)
        {
            await WriteAuditAsync(
                userId: requestContext.UserId,
                userNameOrEmail: null,
                action: AuthenticationAuditAction.Logout,
                outcome: AuthenticationAuditOutcome.Failed,
                failureReason: InvalidRefreshTokenReason,
                requestContext,
                cancellationToken);
            return;
        }

        if (storedRefreshToken.RevokedAtUtc is not null)
        {
            await WriteAuditAsync(
                userId: storedRefreshToken.UserId,
                userNameOrEmail: storedRefreshToken.User.UserName,
                action: AuthenticationAuditAction.Logout,
                outcome: AuthenticationAuditOutcome.Failed,
                failureReason: RevokedRefreshTokenReason,
                requestContext,
                cancellationToken);
            return;
        }

        var now = DateTimeOffset.UtcNow;
        storedRefreshToken.RevokedAtUtc = now;
        dbContext.AuthenticationAuditEntries.Add(CreateAuditEntry(
            storedRefreshToken.UserId,
            storedRefreshToken.User.UserName,
            requestContext.ProjectId,
            AuthenticationAuditAction.Logout,
            AuthenticationAuditOutcome.Succeeded,
            failureReason: null,
            requestContext,
            now));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<AuthUserProfile?> GetUserAsync(
        Guid userId,
        string? authorizationHeader,
        Guid? activeProjectId,
        CancellationToken cancellationToken)
    {
        var user = await LoadUserQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(currentUser => currentUser.Id == userId, cancellationToken);

        return user is null ? null : await MapUserAsync(user, authorizationHeader, activeProjectId, cancellationToken);
    }

    private IQueryable<User> LoadUserQuery()
    {
        return dbContext.Users
            .Include(user => user.RoleAssignments)
                .ThenInclude(assignment => assignment.Role)
                    .ThenInclude(role => role.Permissions)
            .AsSplitQuery();
    }

    private async Task<AuthSession> IssueSessionAsync(
        User user,
        RefreshToken? currentRefreshToken,
        Guid? activeProjectId,
        CancellationToken cancellationToken)
    {
        EnsureJwtOptions();

        var now = DateTimeOffset.UtcNow;
        var accessTokenExpiresAtUtc = now.AddMinutes(Math.Max(1, jwtOptions.AccessTokenMinutes));
        var identityTokenExpiresAtUtc = now.AddMinutes(Math.Max(1, jwtOptions.IdentityTokenMinutes));
        var refreshTokenExpiresAtUtc = now.AddDays(Math.Max(1, jwtOptions.RefreshTokenDays));

        var accessToken = CreateJwtToken(user, "access", jwtOptions.Audience, accessTokenExpiresAtUtc);
        var identityToken = CreateJwtToken(user, "identity", jwtOptions.IdentityAudience, identityTokenExpiresAtUtc);
        var rawRefreshToken = CreateOpaqueToken();
        var refreshTokenHash = HashToken(rawRefreshToken);

        if (currentRefreshToken is not null)
        {
            currentRefreshToken.ReplacedByTokenHash = refreshTokenHash;
            currentRefreshToken.RevokedAtUtc ??= now;
        }

        dbContext.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = refreshTokenHash,
            CreatedAtUtc = now,
            ExpiresAtUtc = refreshTokenExpiresAtUtc
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        return new AuthSession(
            await MapUserAsync(user, $"Bearer {accessToken}", activeProjectId, cancellationToken),
            new AuthTokenBundle(
                accessToken,
                accessTokenExpiresAtUtc,
                identityToken,
                identityTokenExpiresAtUtc,
                rawRefreshToken,
                refreshTokenExpiresAtUtc));
    }

    private string CreateJwtToken(User user, string tokenUse, string audience, DateTimeOffset expiresAtUtc)
    {
        var globalRoleNames = user.RoleAssignments
            .Where(assignment =>
                assignment.ProjectId is null
                && assignment.Role.Scope == RoleScope.Global
                && assignment.Role.ProjectId is null)
            .Select(assignment => assignment.Role.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(BuiltInRoleNames.GetDisplayOrder)
            .ThenBy(roleName => roleName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, user.UserName),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new("token_use", tokenUse)
        };

        claims.AddRange(globalRoleNames.Select(roleName => new Claim("role", roleName)));

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: jwtOptions.Issuer,
            audience: audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAtUtc.UtcDateTime,
            signingCredentials: credentials);

        return tokenHandler.WriteToken(token);
    }

    private async Task<AuthUserProfile> MapUserAsync(
        User user,
        string? authorizationHeader,
        Guid? requestedProjectId,
        CancellationToken cancellationToken)
    {
        var globalAssignments = user.RoleAssignments
            .Where(assignment =>
                assignment.ProjectId is null
                && assignment.Role.Scope == RoleScope.Global
                && assignment.Role.ProjectId is null)
            .ToArray();
        var projectAssignments = user.RoleAssignments
            .Where(assignment =>
                assignment.ProjectId.HasValue
                && assignment.Role.Scope == RoleScope.Project
                && assignment.Role.ProjectId == assignment.ProjectId)
            .ToArray();

        var hasGlobalFullAccess = globalAssignments.Any(assignment => assignment.Role.HasAllPermissions);
        var assignedProjectProfiles = projectAssignments
            .GroupBy(assignment => assignment.ProjectId!.Value)
            .Select(group =>
            {
                var projectId = group.Key;
                var projectName = group.Select(assignment => assignment.Role.ProjectName)
                    .FirstOrDefault(projectName => !string.IsNullOrWhiteSpace(projectName))
                    ?? projectId.ToString();

                return new AuthProjectProfile(projectId, projectName);
            });
        var knownProjectProfiles = hasGlobalFullAccess
            ? await LoadKnownProjectsAsync(cancellationToken)
            : [];
        var projectProfiles = assignedProjectProfiles
            .Concat(knownProjectProfiles)
            .GroupBy(project => project.Id)
            .Select(group => group.First())
            .OrderBy(project => project.Name)
            .ToArray();

        var activeProject = projectProfiles.FirstOrDefault(project => project.Id == requestedProjectId)
            ?? projectProfiles.FirstOrDefault();

        var activeProjectAssignments = activeProject is null
            ? []
            : projectAssignments
                .Where(assignment =>
                    assignment.ProjectId == activeProject.Id
                    && assignment.Role.Scope == RoleScope.Project
                    && assignment.Role.ProjectId == activeProject.Id)
                .ToArray();

        var globalRoles = globalAssignments
            .Select(assignment => assignment.Role.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(BuiltInRoleNames.GetDisplayOrder)
            .ThenBy(roleName => roleName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var activeProjectRoles = activeProjectAssignments
            .Select(assignment => assignment.Role.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(BuiltInRoleNames.GetDisplayOrder)
            .ThenBy(roleName => roleName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var globalPermissions = globalAssignments
            .SelectMany(assignment => assignment.Role.Permissions.Where(permission => permission.ProjectId == null))
            .Select(permission => permission.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order()
            .ToArray();

        var activeProjectPermissions = activeProject is null
            ? []
            : activeProjectAssignments
                .SelectMany(assignment => assignment.Role.Permissions.Where(permission => permission.ProjectId == activeProject.Id))
                .Select(permission => permission.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Order()
                .ToArray();

        var hasAllPermissions = hasGlobalFullAccess || activeProjectAssignments.Any(assignment => assignment.Role.HasAllPermissions);

        var effectivePermissions = globalPermissions
            .Concat(activeProjectPermissions)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order()
            .ToArray();

        return new AuthUserProfile(
            user.Id,
            user.UserName,
            user.Email,
            hasGlobalFullAccess,
            globalRoles,
            globalPermissions,
            projectProfiles,
            activeProject?.Id,
            activeProject?.Name,
            activeProjectPermissions,
            globalRoles
                .Concat(activeProjectRoles)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(BuiltInRoleNames.GetDisplayOrder)
                .ThenBy(roleName => roleName, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            hasAllPermissions,
            effectivePermissions);
    }

    private async Task<IReadOnlyList<AuthProjectProfile>> LoadKnownProjectsAsync(CancellationToken cancellationToken)
    {
        var roles = await dbContext.Roles
            .AsNoTracking()
            .Where(role => role.ProjectId.HasValue)
            .Select(role => new { ProjectId = role.ProjectId!.Value, role.ProjectName })
            .ToListAsync(cancellationToken);
        var permissions = await dbContext.Permissions
            .AsNoTracking()
            .Where(permission => permission.ProjectId.HasValue)
            .Select(permission => new { ProjectId = permission.ProjectId!.Value, permission.ProjectName })
            .ToListAsync(cancellationToken);

        return roles
            .Select(role => (role.ProjectId, role.ProjectName))
            .Concat(permissions.Select(permission => (permission.ProjectId, permission.ProjectName)))
            .Distinct()
            .Select(project => new AuthProjectProfile(
                project.ProjectId,
                string.IsNullOrWhiteSpace(project.ProjectName) ? project.ProjectId.ToString() : project.ProjectName!))
            .ToArray();
    }

    private void EnsureJwtOptions()
    {
        if (string.IsNullOrWhiteSpace(jwtOptions.Issuer)
            || string.IsNullOrWhiteSpace(jwtOptions.Audience)
            || string.IsNullOrWhiteSpace(jwtOptions.IdentityAudience)
            || string.IsNullOrWhiteSpace(jwtOptions.SigningKey))
        {
            throw new InvalidOperationException("JWT options are not configured correctly.");
        }
    }

    private static string CreateOpaqueToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    }

    private static string HashToken(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash);
    }

    private async Task WriteAuditAsync(
        Guid? userId,
        string? userNameOrEmail,
        AuthenticationAuditAction action,
        AuthenticationAuditOutcome outcome,
        string? failureReason,
        RequestContext requestContext,
        CancellationToken cancellationToken)
    {
        dbContext.AuthenticationAuditEntries.Add(CreateAuditEntry(
            userId,
            userNameOrEmail,
            requestContext.ProjectId,
            action,
            outcome,
            failureReason,
            requestContext,
            DateTimeOffset.UtcNow));

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static AuthenticationAuditEntry CreateAuditEntry(
        Guid? userId,
        string? userNameOrEmail,
        Guid? projectId,
        AuthenticationAuditAction action,
        AuthenticationAuditOutcome outcome,
        string? failureReason,
        RequestContext requestContext,
        DateTimeOffset occurredAtUtc)
    {
        return new AuthenticationAuditEntry
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            UserNameOrEmail = TrimToLength(userNameOrEmail, 256),
            ProjectId = projectId,
            Action = action,
            Outcome = outcome,
            FailureReason = TrimToLength(failureReason, 256),
            ClientApplication = TrimToLength(requestContext.ClientApplication, 64),
            IpAddress = TrimToLength(requestContext.IpAddress, 64),
            UserAgent = TrimToLength(requestContext.UserAgent, 512),
            OccurredAtUtc = occurredAtUtc
        };
    }

    private static string? NormalizeIdentifier(string? userNameOrEmail)
    {
        return string.IsNullOrWhiteSpace(userNameOrEmail) ? null : userNameOrEmail.Trim();
    }

    private static string? TrimToLength(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
