using HotChocolate;
using HotChocolate.Authorization;
using HotChocolate.Types;

using Microsoft.EntityFrameworkCore;
using TechtonicCmsApi.Contexts;
using TechtonicCmsApi.Schema.TechtonicCms;
using TechtonicCmsApi.Schema.TechtonicCms.Enums;
using TechtonicCmsApi.Security;

using RoleEntity = TechtonicCmsApi.Schema.TechtonicCms.Entities.Role;

namespace TechtonicCmsApi.Types.Roles;

public class RoleQuery
{
    [Authorize]
    [AbacRequirePermission(BaseResource.Roles, PermissionAction.Read)]
    public async Task<RoleEntity?> Role(
        Guid? id,
        string? name,
        [Service] TechtonicCmsDbContext db)
    {
        if (id is null && string.IsNullOrEmpty(name))
            throw new GraphQLException(ErrorBuilder.New()
                .SetMessage("Must provide either id or name")
                .SetCode("BAD_REQUEST")
                .Build());

        IQueryable<RoleEntity> query = db.Roles;

        if (id.HasValue)
            query = query.Where(r => r.Id == id.Value);
        else if (!string.IsNullOrEmpty(name))
            query = query.Where(r => r.Name == name);

        return await query.FirstOrDefaultAsync();
    }

    [Authorize]
    [AbacRequirePermission(BaseResource.Roles, PermissionAction.Read)]
    [UsePaging(MaxPageSize = 100)]
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public IQueryable<RoleEntity> Roles(
        [Service] TechtonicCmsDbContext db)
    {
        IQueryable<RoleEntity> query = db.Roles;

        return query;
    }
}

[ExtendObjectType(nameof(Query))]
public static class RoleQueries
{
    public static RoleQuery Roles() => new();
}
