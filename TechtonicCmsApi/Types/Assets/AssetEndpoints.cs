using TechtonicCmsApi.Contexts;
using TechtonicCmsApi.Schema.TechtonicCms.Entities;
using TechtonicCmsApi.Schema.TechtonicCms.Enums;
using TechtonicCmsApi.Services;

namespace TechtonicCmsApi.Types.Assets;

public static class AssetEndpoints
{
    public static WebApplication MapAssetEndpoints(this WebApplication app)
    {
        app.MapPost("/assets/upload", async (
            HttpContext context,
            IFormFile file,
            string? alt,
            string? caption,
            bool? isPublic,
            TechtonicCmsDbContext db,
            S3Service s3Service,
            AbacService abacService) =>
        {
            var userIdStr = context.User.FindFirst("userId")?.Value;
            if (userIdStr is null || !Guid.TryParse(userIdStr, out var userId))
                return Results.Unauthorized();

            await abacService.RequirePermissionAsync(userId, BaseResource.Assets, PermissionAction.Upload);

            await using var stream = file.OpenReadStream();
            var filename = file.FileName ?? "upload";
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
            return Results.Ok(asset);
        }).DisableAntiforgery();

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
                    PermissionAction.Download,
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
