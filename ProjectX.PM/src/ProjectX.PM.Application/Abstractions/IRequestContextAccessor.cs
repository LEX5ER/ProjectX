namespace ProjectX.PM.Application.Abstractions;

public sealed record RequestContext(
    string? AuthorizationHeader,
    string? ProjectId);

public interface IRequestContextAccessor
{
    RequestContext GetCurrent();
}
