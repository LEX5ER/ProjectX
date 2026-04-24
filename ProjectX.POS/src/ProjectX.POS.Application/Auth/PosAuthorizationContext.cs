namespace ProjectX.POS.Application.Auth;

public sealed record PosAuthorizationContext(
    Guid UserId,
    string UserName,
    bool HasGlobalFullAccess,
    bool HasGlobalCatalogRead,
    bool HasGlobalCatalogWrite,
    Guid? ActiveProjectId,
    string? ActiveProjectName,
    bool HasActiveProjectRead,
    bool HasActiveProjectWrite)
{
    public bool CanReadAllProducts => HasGlobalFullAccess || HasGlobalCatalogRead;

    public bool CanManageAllProducts => HasGlobalFullAccess || HasGlobalCatalogWrite;

    public bool CanReadAnyProjectData => CanReadAllProducts || (ActiveProjectId.HasValue && (HasActiveProjectRead || HasActiveProjectWrite));

    public bool CanManageAnyProjectData => CanManageAllProducts || (ActiveProjectId.HasValue && HasActiveProjectWrite);

    public bool CanReadAnyProduct =>
        CanReadAllProducts
        || (ActiveProjectId.HasValue && (HasActiveProjectRead || HasActiveProjectWrite));

    public bool CanCreateProduct =>
        CanManageAllProducts
        || (ActiveProjectId.HasValue && HasActiveProjectWrite);

    public bool CanReadProduct(Guid projectId)
    {
        return CanReadAllProducts
            || (ActiveProjectId == projectId && (HasActiveProjectRead || HasActiveProjectWrite));
    }

    public bool CanManageProduct(Guid projectId)
    {
        return CanManageAllProducts
            || (ActiveProjectId == projectId && HasActiveProjectWrite);
    }

    public bool CanReadProject(Guid projectId)
    {
        return CanReadAllProducts
            || (ActiveProjectId == projectId && (HasActiveProjectRead || HasActiveProjectWrite));
    }

    public bool CanManageProject(Guid projectId)
    {
        return CanManageAllProducts
            || (ActiveProjectId == projectId && HasActiveProjectWrite);
    }
}
