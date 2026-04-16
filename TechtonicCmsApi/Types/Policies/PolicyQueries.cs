using HotChocolate;
using HotChocolate.Authorization;
using HotChocolate.Types;

using Microsoft.EntityFrameworkCore;
using TechtonicCmsApi.Contexts;
using TechtonicCmsApi.Schema.TechtonicCms;
using TechtonicCmsApi.Schema.TechtonicCms.Enums;

using PolicyEntity = TechtonicCmsApi.Schema.TechtonicCms.Entities.AbacPolicy;

namespace TechtonicCmsApi.Types.Policies;

public class PolicyQuery
{
    [Authorize(Policy = "Users:Read")]
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

    [Authorize(Policy = "Users:Read")]
    public IQueryable<PolicyEntity> Policies(
        string? search,
        BaseResource? resourceType,
        PermissionAction? actionType,
        PermissionEffect? effect,
        bool? isActive,
        int? limit,
        int? offset,
        [Service] TechtonicCmsDbContext db)
    {
        IQueryable<PolicyEntity> query = db.AbacPolicies;

        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(p => p.Name.Contains(search));
        }

        if (resourceType.HasValue)
        {
            query = query.Where(p => p.ResourceType == resourceType.Value);
        }

        if (actionType.HasValue)
        {
            query = query.Where(p => p.ActionType == actionType.Value);
        }

        if (effect.HasValue)
        {
            query = query.Where(p => p.Effect == effect.Value);
        }

        if (isActive.HasValue)
        {
            query = query.Where(p => p.IsActive == isActive.Value);
        }

        if (offset.HasValue)
        {
            query = query.Skip(offset.Value);
        }

        if (limit.HasValue)
        {
            query = query.Take(limit.Value);
        }

        return query;
    }
}

[ExtendObjectType(nameof(Query))]
public static class PolicyQueries
{
    public static PolicyQuery Policy() => new();
}
