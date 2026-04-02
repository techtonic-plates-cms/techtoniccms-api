using HotChocolate;
using HotChocolate.Authorization;
using HotChocolate.Types;

using Microsoft.EntityFrameworkCore;
using TechtonicCmsApi.Contexts;
using TechtonicCmsApi.Schema.TechtonicCms;
using TechtonicCmsApi.Schema.TechtonicCms.Enums;
using TechtonicCmsApi.Services;

using UserEntity = TechtonicCmsApi.Schema.TechtonicCms.Entities.User;

namespace TechtonicCmsApi.Types.Users;

public class UserQuery
{
    [Authorize]
    public async Task<UserEntity?> User(
        Guid? id,
        string? name,
        [Service] TechtonicCmsDbContext db,
        [Service] AbacService abacService,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = GetUserId(httpContextAccessor);
        await abacService.RequirePermissionAsync(userId, BaseResource.Users, PermissionAction.Read);

        if (id is null && string.IsNullOrEmpty(name))
            throw new GraphQLException(ErrorBuilder.New()
                .SetMessage("Must provide either id or name")
                .SetCode("BAD_REQUEST")
                .Build());

        IQueryable<UserEntity> query = db.Users;

        if (id.HasValue)
            query = query.Where(u => u.Id == id.Value);
        else if (!string.IsNullOrEmpty(name))
            query = query.Where(u => u.Name == name);

        return await query.FirstOrDefaultAsync();
    }

    [Authorize]
    [UsePaging(MaxPageSize = 100)]
    [UseFiltering]
    [UseSorting]
    public async Task<IEnumerable<UserEntity>> Users(
        string? search,
        UserStatus? status,
        int? limit,
        int? offset,
        [Service] TechtonicCmsDbContext db,
        [Service] AbacService abacService,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = GetUserId(httpContextAccessor);
        await abacService.RequirePermissionAsync(userId, BaseResource.Users, PermissionAction.Read);

        IQueryable<UserEntity> query = db.Users;

        if (!string.IsNullOrEmpty(search))
            query = query.Where(u => u.Name.Contains(search));

        if (status.HasValue)
            query = query.Where(u => u.Status == status.Value);

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
public static class UserQueries
{
    public static UserQuery Users() => new();
}
