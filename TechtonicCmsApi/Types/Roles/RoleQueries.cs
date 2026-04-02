using HotChocolate;
using HotChocolate.Authorization;
using HotChocolate.Types;

using Microsoft.EntityFrameworkCore;
using TechtonicCmsApi.Contexts;
using TechtonicCmsApi.Schema.TechtonicCms;

using RoleEntity = TechtonicCmsApi.Schema.TechtonicCms.Entities.Role;

namespace TechtonicCmsApi.Types.Roles;

public class RoleQuery
{
    [Authorize(Policy = "Users:Read")]
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

    [Authorize(Policy = "Users:Read")]
    [UsePaging(MaxPageSize = 100)]
    [UseFiltering]
    [UseSorting]
    public async Task<IEnumerable<RoleEntity>> Roles(
        string? search,
        int? limit,
        int? offset,
        [Service] TechtonicCmsDbContext db)
    {
        IQueryable<RoleEntity> query = db.Roles;

        if (!string.IsNullOrEmpty(search))
            query = query.Where(r => r.Name.Contains(search));

        if (offset.HasValue)
            query = query.Skip(offset.Value);

        if (limit.HasValue)
            query = query.Take(limit.Value);

        return await query.ToListAsync();
    }
}

[ExtendObjectType(nameof(Query))]
public static class RoleQueries
{
    public static RoleQuery Roles() => new();
}
