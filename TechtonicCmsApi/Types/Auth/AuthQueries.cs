using HotChocolate.Authorization;

using Microsoft.EntityFrameworkCore;

using TechtonicCmsApi.Schema.TechtonicCms;
using TechtonicCmsApi.Schema.TechtonicCms.Entities;
using TechtonicCmsApi.Services;

namespace TechtonicCmsApi.Types.Auth;

[ExtendObjectType(typeof(Query))]
public static class AuthQueries
{
    [Authorize]
    public static async Task<User?> Me(
        [Service] TechtonicCmsDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userIdStr = httpContextAccessor.HttpContext?.User.FindFirst("userId")?.Value;
        if (userIdStr is null || !Guid.TryParse(userIdStr, out var userId))
            return null;

        return await db.Users.FindAsync(userId);
    }
}
