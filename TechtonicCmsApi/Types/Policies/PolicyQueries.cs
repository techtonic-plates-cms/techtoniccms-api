using HotChocolate;
using HotChocolate.Authorization;
using HotChocolate.Types;

using Microsoft.EntityFrameworkCore;
using TechtonicCmsApi.Contexts;
using TechtonicCmsApi.Schema.TechtonicCms;
using TechtonicCmsApi.Schema.TechtonicCms.Enums;
using TechtonicCmsApi.Security;

using PolicyEntity = TechtonicCmsApi.Schema.TechtonicCms.Entities.AbacPolicy;

namespace TechtonicCmsApi.Types.Policies;

public class PolicyQuery
{
    [Authorize]
    [AbacRequirePermission(BaseResource.Policies, PermissionAction.Read)]
    public async Task<PolicyEntity?> Policy(
        Guid? id,
        string? name,
        [Service] TechtonicCmsDbContext db)
    {
        if (id is null && string.IsNullOrEmpty(name))
            throw new GraphQLException(ErrorBuilder.New()
                .SetMessage("Must provide either id or name")
                .SetCode("BAD_REQUEST")
                .Build());

        IQueryable<PolicyEntity> query = db.AbacPolicies;

        if (id.HasValue)
            query = query.Where(p => p.Id == id.Value);
        else if (!string.IsNullOrEmpty(name))
            query = query.Where(p => p.Name == name);

        return await query.FirstOrDefaultAsync();
    }

    [Authorize]
    [AbacRequirePermission(BaseResource.Policies, PermissionAction.Read)]
    [UsePaging(MaxPageSize = 100)]
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public IQueryable<PolicyEntity> Policies(
        [Service] TechtonicCmsDbContext db)
    {
        IQueryable<PolicyEntity> query = db.AbacPolicies;


  

        return query;
    }
}

[ExtendObjectType(nameof(Query))]
public static class PolicyQueries
{
    public static PolicyQuery Policy() => new();
}
