using System.Net.Http.Headers;
using System.Net.Http.Json;
using ProjectX.POS.Application.Abstractions;
using ProjectX.POS.Application.Auth;

namespace ProjectX.POS.Infrastructure.Auth;

public sealed class IamAuthorizationContextService(HttpClient httpClient) : IIamAuthorizationContextService
{
    public async Task<PosAuthorizationContext> GetCurrentAsync(RequestContext requestContext, CancellationToken cancellationToken)
    {
        var authorizationHeader = requestContext.AuthorizationHeader;

        if (string.IsNullOrWhiteSpace(authorizationHeader))
        {
            throw new InvalidOperationException("Authorization header is missing.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Authorization = AuthenticationHeaderValue.Parse(authorizationHeader);

        if (!string.IsNullOrWhiteSpace(requestContext.ProjectId))
        {
            request.Headers.TryAddWithoutValidation(ProjectContextHeaderNames.ProjectId, requestContext.ProjectId);
        }

        using var response = await httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"IAM authorization lookup failed with status code {(int)response.StatusCode}.");
        }

        var iamUser = await response.Content.ReadFromJsonAsync<IamUserResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("IAM authorization lookup returned an empty response.");

        return Map(iamUser);
    }

    private static PosAuthorizationContext Map(IamUserResponse user)
    {
        var hasCatalogRead = user.HasGlobalFullAccess
            || user.GlobalPermissions.Contains("projects.read", StringComparer.OrdinalIgnoreCase)
            || user.GlobalPermissions.Contains("projects.write", StringComparer.OrdinalIgnoreCase);
        var hasCatalogWrite = user.HasGlobalFullAccess
            || user.GlobalPermissions.Contains("projects.write", StringComparer.OrdinalIgnoreCase);
        var hasActiveProjectWrite = user.HasAllPermissions
            || user.ActiveProjectPermissions.Contains("projects.write", StringComparer.OrdinalIgnoreCase);
        var hasActiveProjectRead = hasActiveProjectWrite
            || user.ActiveProjectPermissions.Contains("projects.read", StringComparer.OrdinalIgnoreCase);

        return new PosAuthorizationContext(
            user.HasGlobalFullAccess,
            hasCatalogRead,
            hasCatalogWrite,
            user.ActiveProjectId,
            user.ActiveProjectName,
            hasActiveProjectRead,
            hasActiveProjectWrite);
    }

    private sealed record IamProjectResponse(
        Guid Id,
        string Name);

    private sealed record IamUserResponse(
        Guid Id,
        string UserName,
        string Email,
        bool HasGlobalFullAccess,
        IamProjectResponse[] Projects,
        Guid? ActiveProjectId,
        string? ActiveProjectName,
        string[] GlobalRoles,
        string[] GlobalPermissions,
        string[] ActiveProjectPermissions,
        string[] Roles,
        bool HasAllPermissions,
        string[] Permissions);
}
