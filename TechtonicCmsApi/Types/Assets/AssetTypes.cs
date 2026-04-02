using HotChocolate;
using HotChocolate.Types;

using Microsoft.EntityFrameworkCore;

using TechtonicCmsApi.Contexts;
using TechtonicCmsApi.Schema.TechtonicCms.Entities;

namespace TechtonicCmsApi.Types.Assets;

[ObjectType<Asset>]
public static partial class AssetType
{
    public static string GetId([Parent] Asset asset) => asset.Id.ToString();

    [GraphQLType<NonNullType<StringType>>]
    public static string GetFilename([Parent] Asset asset) => asset.Filename;

    [GraphQLType<NonNullType<StringType>>]
    public static string GetMimeType([Parent] Asset asset) => asset.MimeType;

    public static int GetFileSize([Parent] Asset asset) => asset.FileSize;

    [GraphQLType<NonNullType<StringType>>]
    public static string GetPath([Parent] Asset asset) => asset.Path;

    public static string GetUploadedBy([Parent] Asset asset) => asset.UploadedBy.ToString();

    public static string GetUploadedAt([Parent] Asset asset) =>
        asset.UploadedAt.ToUniversalTime().ToString("o");

    public static string? GetAlt([Parent] Asset asset) => asset.Alt;

    public static string? GetCaption([Parent] Asset asset) => asset.Caption;

    public static bool GetIsPublic([Parent] Asset asset) => asset.IsPublic;

    public static string GetUrl(
        [Parent] Asset asset,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var request = httpContextAccessor.HttpContext?.Request;
        if (request is null) return $"/assets/{asset.Id}";
        return $"{request.Scheme}://{request.Host}/assets/{asset.Id}";
    }

    public static async Task<User?> GetUploadedByUser(
        [Parent] Asset asset,
        [Service] TechtonicCmsDbContext db)
    {
        return await db.Users.FindAsync(asset.UploadedBy);
    }
}
