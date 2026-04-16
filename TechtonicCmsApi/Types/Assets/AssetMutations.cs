using HotChocolate;
using HotChocolate.Authorization;
using HotChocolate.Types;

using TechtonicCmsApi.Contexts;
using TechtonicCmsApi.Schema.TechtonicCms.Entities;
using TechtonicCmsApi.Schema.TechtonicCms.Enums;
using TechtonicCmsApi.Services;

namespace TechtonicCmsApi.Types.Assets;

public class UpdateAssetInput
{
    [GraphQLType<NonNullType<IdType>>]
    public Guid Id { get; set; }
    public string? Alt { get; set; }
    public string? Caption { get; set; }
    public bool? IsPublic { get; set; }
}

public class AssetMutation
{
    [Authorize]
    public async Task<Asset> Update(
        UpdateAssetInput input,
        [Service] TechtonicCmsDbContext db,
        [Service] AbacService abacService,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = GetUserId(httpContextAccessor);
        var asset = await db.Assets.FindAsync(input.Id);
        if (asset is null)
            throw new GraphQLException(ErrorBuilder.New()
                .SetMessage($"Asset with id {input.Id} not found")
                .SetCode("NOT_FOUND")
                .Build());
        await abacService.RequirePermissionAsync(
            userId,
            BaseResource.Assets,
            PermissionAction.Update,
            new() {
                { "ResourceAssetId", asset.Id.ToString() },
                { "ResourceAssetUploadedBy", asset.UploadedBy.ToString() },
                { "ResourceAssetMimeType", asset.MimeType },
                { "ResourceAssetFileSize", asset.FileSize },
            });
        if (input.Alt is not null)
            asset.Alt = input.Alt;
        if (input.Caption is not null)
            asset.Caption = input.Caption;
        if (input.IsPublic.HasValue)
            asset.IsPublic = input.IsPublic.Value;
        await db.SaveChangesAsync();
        return asset;
    }

    [Authorize]
    public async Task<bool> Delete(
        Guid id,
        [Service] TechtonicCmsDbContext db,
        [Service] S3Service s3Service,
        [Service] AbacService abacService,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = GetUserId(httpContextAccessor);
        var asset = await db.Assets.FindAsync(id);
        if (asset is null)
            throw new GraphQLException(ErrorBuilder.New()
                .SetMessage($"Asset with id {id} not found")
                .SetCode("NOT_FOUND")
                .Build());
        await abacService.RequirePermissionAsync(
            userId,
            BaseResource.Assets,
            PermissionAction.Delete,
            new() {
                { "ResourceAssetId", asset.Id.ToString() },
                { "ResourceAssetUploadedBy", asset.UploadedBy.ToString() },
                { "ResourceAssetMimeType", asset.MimeType },
                { "ResourceAssetFileSize", asset.FileSize },
            });
        try
        {
            await s3Service.DeleteAsync(asset.Path);
        }
        catch
        {
        }
        db.Assets.Remove(asset);
        await db.SaveChangesAsync();
        return true;
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

[ExtendObjectType(nameof(Mutation))]
public static class AssetMutations
{
    public static AssetMutation Assets() => new();
}
