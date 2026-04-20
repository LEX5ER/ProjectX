namespace ProjectX.IAM.Application.Abstractions;

public sealed record RequestContext(
    Guid? UserId,
    Guid? ProjectId,
    string? AuthorizationHeader,
    string? ClientApplication = null,
    string? IpAddress = null,
    string? UserAgent = null);

public interface IRequestContextAccessor
{
    RequestContext GetCurrent();
}
