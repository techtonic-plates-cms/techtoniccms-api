using HotChocolate;
using HotChocolate.Authorization;
using HotChocolate.Types;

using Microsoft.EntityFrameworkCore;
using TechtonicCmsApi.Contexts;
using TechtonicCmsApi.Schema.TechtonicCms.Entities;

namespace TechtonicCmsApi.Types.Collections;

public class CollectionQuery
{
 /*   [Authorize]
    public async Task<Collection?> Collection(
        Guid? id,
        string? slug,
        [Service] TechtonicCmsDbContext db)
    {
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

        return await query.FirstOrDefaultAsync();
    }

    [Authorize]
    [UsePaging(MaxPageSize = 100)]
    [UseFiltering]
    [UseSorting]
    public async Task<IEnumerable<Collection>> Collections(
        string? search,
        int? limit,
        int? offset,
        [Service] TechtonicCmsDbContext db)
    {
        IQueryable<Collection> query = db.Collections;

        if (!string.IsNullOrEmpty(search))
            query = query.Where(c => c.Name.Contains(search) || c.Slug.Contains(search));

        if (offset.HasValue)
            query = query.Skip(offset.Value);

        if (limit.HasValue)
            query = query.Take(limit.Value);

        return await query.OrderBy(c => c.Name).ToListAsync();
    }*/
}

[ExtendObjectType(nameof(Query))]
public static class CollectionQueries
{
    public static CollectionQuery Collections() => new();
}
