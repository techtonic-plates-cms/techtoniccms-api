using HotChocolate;

using TechtonicCmsApi.Contexts;
using TechtonicCmsApi.Schema.TechtonicCms.Entities;
using TechtonicCmsApi.Schema.TechtonicCms.Enums;
using TechtonicCmsApi.Services;

namespace TechtonicCmsApi.Types.Assets;

public static class AssetEndpoints
{
    public static WebApplication MapAssetEndpoints(this WebApplication app)
    {
        var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "jpg", "jpeg", "png", "gif", "webp", "svg", "pdf",
            "doc", "docx", "txt", "csv", "mp3", "mp4", "ppt", "pptx", "xls", "xlsx", "avi", "mov", "mkv", "md"
        };

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

            const long maxFileSize = 50 * 1024 * 1024;
            if (file.Length > maxFileSize)
                throw new GraphQLException(ErrorBuilder.New()
                    .SetMessage("File exceeds maximum size of 50MB")
                    .SetCode("BAD_REQUEST")
                    .Build());

            var filename = file.FileName ?? "upload";
            var ext = System.IO.Path.GetExtension(filename).TrimStart('.').ToLowerInvariant();
            if (!allowedExtensions.Contains(ext))
                throw new GraphQLException(ErrorBuilder.New()
                    .SetMessage("File type not allowed")
                    .SetCode("BAD_REQUEST")
                    .Build());

            var expectedContentType = s3Service.GetContentType(filename);
            var contentType = string.IsNullOrEmpty(file.ContentType)
                ? expectedContentType
                : file.ContentType;

            var imageContentTypes = new[] { "image/jpeg", "image/png", "image/gif", "image/webp", "image/svg+xml" };
            if (imageContentTypes.Contains(contentType) || imageContentTypes.Contains(expectedContentType))
            {
                await using var magicStream = file.OpenReadStream();
                var header = new byte[8];
                var read = await magicStream.ReadAsync(header.AsMemory(0, 8));
                if (read >= 2)
                {
                    bool validMagic = contentType switch
                    {
                        "image/jpeg" => header[0] == 0xFF && header[1] == 0xD8,
                        "image/png" => header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47,
                        "image/gif" => (header[0] == 0x47 && header[1] == 0x49 && header[2] == 0x46),
                        "image/webp" => header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46,
                        "image/svg+xml" => true,
                        _ => true
                    };
                    if (!validMagic)
                        throw new GraphQLException(ErrorBuilder.New()
                            .SetMessage("File content does not match claimed type")
                            .SetCode("BAD_REQUEST")
                            .Build());
                }
            }

            await using var stream = file.OpenReadStream();
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
        }).DisableAntiforgery().RequireRateLimiting("Upload");

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

            context.Response.Headers.CacheControl = asset.IsPublic ? "public, max-age=31536000" : "private, max-age=31536000";
            context.Response.Headers.ContentDisposition = $"inline; filename=\"{asset.Filename}\"";
            context.Response.Headers.Append("X-Asset-Id", asset.Id.ToString());

            return Results.Stream(stream, asset.MimeType);
        });

        return app;
    }
}
