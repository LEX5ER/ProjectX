namespace ProjectX.POS.Application.Auth;

public sealed record PosAuthorizationContext(
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
}
