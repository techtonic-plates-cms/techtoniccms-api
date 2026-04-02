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
    public async Task<Asset> Upload(
        IFile file,
        string? alt,
        string? caption,
        bool? isPublic,
        [Service] TechtonicCmsDbContext db,
        [Service] S3Service s3Service,
        [Service] AbacService abacService,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = GetUserId(httpContextAccessor);
        await abacService.RequirePermissionAsync(userId, BaseResource.Assets, PermissionAction.Upload);

        await using var stream = file.OpenReadStream();
        var filename = file.Name ?? "upload";
        var contentType = string.IsNullOrEmpty(file.ContentType)
            ? s3Service.GetContentType(filename)
            : file.ContentType;
        var s3Key = s3Service.GenerateS3Key(filename, userId);
        await s3Service.UploadAsync(s3Key, stream, contentType);

        var asset = new Asset
        {
            Id = Guid.NewGuid(),
            Filename = filename,
            MimeType = contentType,
            FileSize = (int)file.Length,
            Path = s3Key,
            UploadedBy = userId,
            UploadedAt = DateTime.UtcNow,
            Alt = alt,
            Caption = caption,
            IsPublic = isPublic ?? false
        };
        db.Assets.Add(asset);
        await db.SaveChangesAsync();
        return asset;
    }

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
            new() { { "OwnerId", asset.UploadedBy } });
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
            new() { { "OwnerId", asset.UploadedBy } });
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
