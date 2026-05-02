using System.Linq.Expressions;
using System.Reflection;

using HotChocolate;
using HotChocolate.Types;
using HotChocolate.Types.Descriptors;
using Microsoft.EntityFrameworkCore;

using TechtonicCmsApi.Schema.TechtonicCms.Enums;
using TechtonicCmsApi.Services;

namespace TechtonicCmsApi.Security;

/// <summary>
/// Runs after filtering/sorting but before paging executes.
/// If the user has an ownership-restricting ABAC policy, appends an ownership
/// filter to the query so that only rows the user is allowed to see are returned.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class UseAbacRowCheckAttribute : ObjectFieldDescriptorAttribute
{
    public BaseResource Resource { get; }
    public PermissionAction Action { get; }

    public UseAbacRowCheckAttribute(BaseResource resource, PermissionAction action)
    {
        Resource = resource;
        Action = action;
    }

    protected override void OnConfigure(IDescriptorContext context, IObjectFieldDescriptor descriptor, MemberInfo member)
    {
        descriptor.Use(next => async ctx =>
        {
            await next(ctx);

            if (ctx.Result is not IQueryable queryable)
                return;

            var abacService = ctx.Services.GetRequiredService<AbacService>();
            var httpContextAccessor = ctx.Services.GetRequiredService<IHttpContextAccessor>();

            var userIdStr = httpContextAccessor.HttpContext?.User.FindFirst("userId")?.Value;
            if (userIdStr is null || !Guid.TryParse(userIdStr, out var userId))
                throw new GraphQLException(ErrorBuilder.New()
                    .SetMessage("Authentication required")
                    .SetCode("UNAUTHENTICATED")
                    
                    .Build());

            // Determine if the user is restricted to their own resources only
            var isRestricted = await abacService.IsRestrictedToOwnResourcesAsync(userId, Resource, Action);
            if (!isRestricted)
                return;

            // Build an ownership filter: ownershipField == currentUserId
            var ownershipProp = AbacService.GetOwnershipPropertyName(Resource);
            if (ownershipProp is null)
                return;

            var entityType = queryable.ElementType;
            var param = Expression.Parameter(entityType, "x");
            var property = Expression.Property(param, ownershipProp);

            Expression allowedCondition;
            if (property.Type == typeof(Guid))
            {
                allowedCondition = Expression.Equal(property, Expression.Constant(userId));
            }
            else if (property.Type == typeof(string))
            {
                allowedCondition = Expression.Equal(property, Expression.Constant(userId.ToString()));
            }
            else
            {
                // Unsupported ownership property type — fall back to post-materialization
                return;
            }

            var lambda = Expression.Lambda(allowedCondition, param);
            var whereMethod = typeof(Queryable).GetMethods()
                .First(m => m.Name == "Where" && m.GetParameters().Length == 2)
                .MakeGenericMethod(entityType);

            var filteredQuery = whereMethod.Invoke(null, [queryable, lambda]);
            if (filteredQuery is null)
                return;

            ctx.Result = filteredQuery;
        });
    }
}
