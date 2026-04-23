using HotChocolate;
using HotChocolate.Authorization;
using HotChocolate.Types;

using Microsoft.EntityFrameworkCore;
using TechtonicCmsApi.Contexts;
using TechtonicCmsApi.Schema.TechtonicCms;
using TechtonicCmsApi.Schema.TechtonicCms.Enums;
using TechtonicCmsApi.Services;

namespace TechtonicCmsApi.Types.ApiKeys;

public class ApiKeyQuery
{
    [Authorize("ApiKeys:Read")]
    public async Task<Schema.TechtonicCms.Entities.ApiKey?> ApiKey(
        Guid id,
        [Service] TechtonicCmsDbContext db,
        [Service] AbacService abacService,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = GetUserId(httpContextAccessor);

        var apiKey = await db.ApiKeys.FindAsync(id);
        if (apiKey is null)
            throw new GraphQLException(ErrorBuilder.New()
                .SetMessage("API key not found")
                .SetCode("NOT_FOUND")
                .Build());

        await abacService.RequirePermissionAsync(
            userId,
            BaseResource.ApiKeys,
            PermissionAction.Read,
            new Dictionary<string, object?>
            {
                ["ResourceApiKeyUserId"] = apiKey.UserId.ToString(),
            });

        return apiKey;
    }

    [Authorize("ApiKeys:Read")]
    [UsePaging(MaxPageSize = 100)]
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public async Task<IQueryable<Schema.TechtonicCms.Entities.ApiKey>> ApiKeys(
        Guid? userId,
        bool? isActive,
        int? limit,
        int? offset,
        [Service] TechtonicCmsDbContext db,
        [Service] AbacService abacService,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var currentUserId = GetUserId(httpContextAccessor);
        await abacService.RequirePermissionAsync(currentUserId, BaseResource.ApiKeys, PermissionAction.Read);

        IQueryable<Schema.TechtonicCms.Entities.ApiKey> query = db.ApiKeys;

        if (userId.HasValue)
            query = query.Where(a => a.UserId == userId.Value);

        if (isActive.HasValue)
            query = query.Where(a => a.IsActive == isActive.Value);

        if (offset.HasValue)
            query = query.Skip(offset.Value);

        if (limit.HasValue)
            query = query.Take(limit.Value);

        return query;
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
public static class ApiKeyQueries
{
    public static ApiKeyQuery ApiKeys() => new();
}
