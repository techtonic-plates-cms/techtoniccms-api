using HotChocolate;
using HotChocolate.Authorization;
using HotChocolate.Types;

using Microsoft.EntityFrameworkCore;

using TechtonicCmsApi.Contexts;
using TechtonicCmsApi.Schema.TechtonicCms.Entities;
using TechtonicCmsApi.Schema.TechtonicCms.Enums;
using TechtonicCmsApi.Security;
using TechtonicCmsApi.Services;

namespace TechtonicCmsApi.Types.Assets;

public class AssetQuery
{
    [Authorize]
    [UseProjection]
    public async Task<Asset?> Asset(
        Guid id,
        [Service] TechtonicCmsDbContext db,
        [Service] AbacService abacService,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = GetUserId(httpContextAccessor);

        var asset = await db.Assets.FindAsync(id);
        if (asset is null)
            return null;

        await abacService.RequirePermissionAsync(
            userId,
            BaseResource.Assets,
            PermissionAction.Read,
            new() {
                { "ResourceAssetId", asset.Id.ToString() },
                { "ResourceAssetUploadedBy", asset.UploadedBy.ToString() },
                { "ResourceAssetMimeType", asset.MimeType },
                { "ResourceAssetFileSize", asset.FileSize },
            });

        return asset;
    }

    [Authorize]
    [AbacRequirePermission(BaseResource.Assets, PermissionAction.Read)]
    [UsePaging(MaxPageSize = 100)]
    [UseAbacRowCheck(BaseResource.Assets, PermissionAction.Read)]
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public IQueryable<Asset> Assets(
        [Service] TechtonicCmsDbContext db)
    {
        return db.Assets;
    }

    private static Guid GetUserId(IHttpContextAccessor httpContextAccessor)
    {
        var userIdStr = httpContextAccessor.HttpContext?.User.FindFirst("userId")?.Value;
        if (userIdStr is null || !Guid.TryParse(userIdStr, out var userId))
            throw new GraphQLException(ErrorBuilder.New()
                .SetMessage("Authentication required")
                .SetCode("UNAUTHENTICATED")
                .Build());
        return userId;
    }
}

[ExtendObjectType(nameof(Query))]
public static class AssetQueries
{
    public static AssetQuery Assets() => new();
}
