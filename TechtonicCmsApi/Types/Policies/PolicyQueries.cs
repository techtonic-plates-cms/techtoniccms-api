using HotChocolate;
using HotChocolate.Authorization;
using HotChocolate.Types;

using Microsoft.EntityFrameworkCore;
using TechtonicCmsApi.Contexts;
using TechtonicCmsApi.Schema.TechtonicCms;

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
    public async Task<IEnumerable<PolicyEntity>> Policies(
        string? search,
        string? resourceType,
        string? actionType,
        string? effect,
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

        if (!string.IsNullOrEmpty(resourceType))
        {
            if (Enum.TryParse<Schema.TechtonicCms.Enums.BaseResource>(resourceType, true, out var resourceTypeEnum))
            {
                query = query.Where(p => p.ResourceType == resourceTypeEnum);
            }
        }

        if (!string.IsNullOrEmpty(actionType))
        {
            if (Enum.TryParse<Schema.TechtonicCms.Enums.PermissionAction>(actionType, true, out var actionTypeEnum))
            {
                query = query.Where(p => p.ActionType == actionTypeEnum);
            }
        }

        if (!string.IsNullOrEmpty(effect))
        {
            if (Enum.TryParse<Schema.TechtonicCms.Enums.PermissionEffect>(effect, true, out var effectEnum))
            {
                query = query.Where(p => p.Effect == effectEnum);
            }
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

        return await query.ToListAsync();
    }
}

[ExtendObjectType(nameof(Query))]
public static class PolicyQueries
{
    public static PolicyQuery Policy() => new();
}
