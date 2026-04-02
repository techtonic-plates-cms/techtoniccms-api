using HotChocolate;
using HotChocolate.Authorization;

using Microsoft.EntityFrameworkCore;

using TechtonicCmsApi.Contexts;
using TechtonicCmsApi.Schema.TechtonicCms.Entities;
using TechtonicCmsApi.Schema.TechtonicCms.Enums;
using TechtonicCmsApi.Services;

namespace TechtonicCmsApi.Types.Assets;

public class AssetQuery
{
    [Authorize]
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
            new() { { "OwnerId", asset.UploadedBy } });

        return asset;
    }

    [Authorize]
    public async Task<IEnumerable<Asset>> Assets(
        int? limit,
        int? offset,
        [Service] TechtonicCmsDbContext db,
        [Service] AbacService abacService,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = GetUserId(httpContextAccessor);
        await abacService.RequirePermissionAsync(userId, BaseResource.Assets, PermissionAction.Read);

        IQueryable<Asset> query = db.Assets;

        if (offset.HasValue)
            query = query.Skip(offset.Value);
        if (limit.HasValue)
            query = query.Take(limit.Value);

        return await query.ToListAsync();
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
