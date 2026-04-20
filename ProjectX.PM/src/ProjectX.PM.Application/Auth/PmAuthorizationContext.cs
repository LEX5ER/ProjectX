namespace ProjectX.PM.Application.Auth;

public sealed record PmAuthorizationContext(
    bool HasGlobalFullAccess,
    bool HasGlobalProjectsRead,
    bool HasGlobalProjectsWrite,
    Guid? ActivePmProjectId,
    string? ActiveProjectName,
    bool HasActiveProjectRead,
    bool HasActiveProjectWrite)
{
    public bool CanReadAllProjects => HasGlobalFullAccess || HasGlobalProjectsRead;

    public bool CanCreateProjects => HasGlobalFullAccess || HasGlobalProjectsWrite;

    public bool CanReadAnyProject =>
        CanReadAllProjects
        || (ActivePmProjectId.HasValue && (HasActiveProjectRead || HasActiveProjectWrite));

    public bool CanReadProject(Guid projectId)
    {
        return CanReadAllProjects
            || (ActivePmProjectId == projectId && (HasActiveProjectRead || HasActiveProjectWrite));
    }

    public bool CanWriteProject(Guid projectId)
    {
        return CanCreateProjects
            || (ActivePmProjectId == projectId && HasActiveProjectWrite);
    }
}
