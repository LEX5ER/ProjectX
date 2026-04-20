namespace ProjectX.POS.Application.Abstractions;

public sealed record RequestContext(
    string? AuthorizationHeader,
    string? ProjectId);

public interface IRequestContextAccessor
{
    RequestContext GetCurrent();
}
