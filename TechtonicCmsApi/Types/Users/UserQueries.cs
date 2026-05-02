using HotChocolate;
using HotChocolate.Authorization;
using HotChocolate.Types;

using Microsoft.EntityFrameworkCore;
using TechtonicCmsApi.Contexts;
using TechtonicCmsApi.Schema.TechtonicCms;
using TechtonicCmsApi.Schema.TechtonicCms.Enums;
using TechtonicCmsApi.Security;
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

        var user = await query.FirstOrDefaultAsync();

        await abacService.RequirePermissionAsync(
            userId,
            BaseResource.Users,
            PermissionAction.Read,
            user is null ? null : new Dictionary<string, object?>
            {
                ["ResourceUserId"] = user.Id.ToString(),
                ["ResourceUserStatus"] = user.Status.ToString(),
            });

        return user;
    }

    [Authorize]
    [UsePaging(MaxPageSize = 100)]
    [UseAbacRowCheck(BaseResource.Users, PermissionAction.Read)]
    [UseFiltering]
    [UseSorting]
    public IQueryable<UserEntity> Users(
        [Service] TechtonicCmsDbContext db)
    {
        return db.Users;
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
