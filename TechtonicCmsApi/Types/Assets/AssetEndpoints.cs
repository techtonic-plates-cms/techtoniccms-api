using TechtonicCmsApi.Contexts;
using TechtonicCmsApi.Schema.TechtonicCms.Enums;
using TechtonicCmsApi.Services;

namespace TechtonicCmsApi.Types.Assets;

public static class AssetEndpoints
{
    public static WebApplication MapAssetEndpoints(this WebApplication app)
    {
        app.MapGet("/assets/{id:guid}", async (
            Guid id,
            HttpContext context,
            TechtonicCmsDbContext db,
            S3Service s3Service,
            AbacService abacService) =>
        {
            var asset = await db.Assets.FindAsync(id);
            if (asset is null)
                return Results.NotFound(new { message = "Asset not found" });

            var userIdStr = context.User.FindFirst("userId")?.Value;
            Guid? userId = Guid.TryParse(userIdStr, out var parsed) ? parsed : null;

            if (userId.HasValue)
            {
                var allowed = await abacService.CheckPermissionAsync(
                    userId.Value,
                    BaseResource.Assets,
                    PermissionAction.Read,
                    new() { { "OwnerId", asset.UploadedBy } });

                if (!allowed)
                    return Results.Forbid();
            }
            else
            {
                if (!asset.IsPublic)
                    return Results.Forbid();
            }

            var stream = await s3Service.DownloadAsync(asset.Path);
            if (stream is null)
                return Results.NotFound(new { message = "Asset file not found in storage" });

            context.Response.Headers.CacheControl = "public, max-age=31536000";
            context.Response.Headers.ContentDisposition = $"inline; filename=\"{asset.Filename}\"";
            context.Response.Headers.Append("X-Asset-Id", asset.Id.ToString());

            return Results.Stream(stream, asset.MimeType);
        });

        return app;
    }
}
