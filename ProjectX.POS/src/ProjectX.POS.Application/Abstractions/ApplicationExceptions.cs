namespace ProjectX.POS.Application.Abstractions;

public abstract class ApplicationProblemException(string title, string detail) : Exception(detail)
{
    public string Title { get; } = title;

    public string Detail { get; } = detail;
}

public sealed class ApplicationValidationException(IReadOnlyDictionary<string, string[]> errors)
    : ApplicationProblemException("Validation failed", "One or more validation errors occurred.")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}

public sealed class ApplicationForbiddenException(string detail)
    : ApplicationProblemException("Forbidden", detail);

public sealed class ApplicationConflictException(string detail)
    : ApplicationProblemException("Conflict", detail);

public sealed class ApplicationServiceUnavailableException(string title, string detail)
    : ApplicationProblemException(title, detail);
