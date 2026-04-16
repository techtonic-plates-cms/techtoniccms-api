using HotChocolate;
using HotChocolate.Authorization;
using HotChocolate.Types;

using Microsoft.EntityFrameworkCore;
using TechtonicCmsApi.Contexts;
using TechtonicCmsApi.Schema.TechtonicCms.Entities;
using TechtonicCmsApi.Schema.TechtonicCms.Enums;
using TechtonicCmsApi.Services;

namespace TechtonicCmsApi.Types.Collections;

public class CollectionQuery
{
    [Authorize(Policy = "Collections:Read")]
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

    [Authorize(Policy = "Collections:Read")]
    [UsePaging(MaxPageSize = 100)]
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public async Task<IQueryable<Collection>> CollectionsData(
        string? search,
        int? limit,
        int? offset,
        [Service] TechtonicCmsDbContext db,
        [Service] AbacService abacService,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = GetUserId(httpContextAccessor);
        await abacService.RequirePermissionAsync(userId, BaseResource.Collections, PermissionAction.Read);

        IQueryable<Collection> query = db.Collections;

        if (!string.IsNullOrEmpty(search))
            query = query.Where(c => c.Name.Contains(search) || c.Slug.Contains(search));

        if (offset.HasValue)
            query = query.Skip(offset.Value);

        if (limit.HasValue)
            query = query.Take(limit.Value);

        return query.OrderBy(c => c.Name);
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
