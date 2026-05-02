using HotChocolate;
using HotChocolate.Authorization;
using HotChocolate.Types;

using Microsoft.EntityFrameworkCore;
using TechtonicCmsApi.Contexts;
using TechtonicCmsApi.Schema.TechtonicCms.Entities;
using TechtonicCmsApi.Schema.TechtonicCms.Enums;
using TechtonicCmsApi.Security;
using TechtonicCmsApi.Services;

namespace TechtonicCmsApi.Types.Collections;

public class CollectionQuery
{
    [Authorize(Policy = "Collections:Read")]
    [UseProjection]
    public async Task<Collection?> CollectionData(
        Guid? id,
        string? slug,
        [Service] TechtonicCmsDbContext db,
        [Service] AbacService abacService,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = GetUserId(httpContextAccessor);

        if (id is null && string.IsNullOrEmpty(slug))
            throw new GraphQLException(ErrorBuilder.New()
                .SetMessage("Must provide either id or slug")
                .SetCode("BAD_REQUEST")
                .Build());

        IQueryable<Collection> query = db.Collections;

        if (id.HasValue)
            query = query.Where(c => c.Id == id.Value);
        else if (!string.IsNullOrEmpty(slug))
            query = query.Where(c => c.Slug == slug);

        var collection = await query.FirstOrDefaultAsync();

        await abacService.RequirePermissionAsync(
            userId,
            BaseResource.Collections,
            PermissionAction.Read,
            collection is null ? null : new Dictionary<string, object?>
            {
                ["ResourceCollectionId"] = collection.Id.ToString(),
                ["ResourceCollectionSlug"] = collection.Slug,
                ["ResourceCollectionCreatedBy"] = collection.CreatedBy.ToString(),
                ["ResourceCollectionIsLocalized"] = collection.IsLocalized,
            });

        return collection;
    }

    [Authorize]
    [UsePaging(MaxPageSize = 100)]
    [UseAbacRowCheck(BaseResource.Collections, PermissionAction.Read)]
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public IQueryable<Collection> CollectionsData(
        [Service] TechtonicCmsDbContext db)
    {
        return db.Collections.OrderBy(c => c.Name);
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
public static class CollectionQueries
{
    public static CollectionQuery Collections() => new();
}
