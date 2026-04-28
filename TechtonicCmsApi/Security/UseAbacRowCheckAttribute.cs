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
/// If the user has an ownership-restricting ABAC policy, performs a lightweight
/// secondary query to verify the filtered result set contains no forbidden rows.
/// Throws FORBIDDEN if any row would be denied.
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

            // Build a forbidden-row check query: ownershipField != currentUserId
            var ownershipProp = AbacService.GetOwnershipPropertyName(Resource);
            if (ownershipProp is null)
                return;

            var entityType = queryable.ElementType;
            var param = Expression.Parameter(entityType, "x");
            var property = Expression.Property(param, ownershipProp);

            Expression forbiddenCondition;
            if (property.Type == typeof(Guid))
            {
                forbiddenCondition = Expression.NotEqual(property, Expression.Constant(userId));
            }
            else if (property.Type == typeof(string))
            {
                forbiddenCondition = Expression.NotEqual(property, Expression.Constant(userId.ToString()));
            }
            else
            {
                // Unsupported ownership property type — fall back to post-materialization
                return;
            }

            var lambda = Expression.Lambda(forbiddenCondition, param);
            var whereMethod = typeof(Queryable).GetMethods()
                .First(m => m.Name == "Where" && m.GetParameters().Length == 2)
                .MakeGenericMethod(entityType);

            var forbiddenQuery = whereMethod.Invoke(null, [queryable, lambda]);
            if (forbiddenQuery is null)
                return;

            // Execute: filteredQuery.Where(x => x.Owner != userId).Take(1).AnyAsync()
            var anyMethod = typeof(EntityFrameworkQueryableExtensions)
                .GetMethods()
                .First(m => m.Name == "AnyAsync" && m.GetParameters().Length == 2)
                .MakeGenericMethod(entityType);

            var cancellationToken = ctx.RequestAborted;
            var task = (Task)anyMethod.Invoke(null, [forbiddenQuery, cancellationToken])!;
            await task;

            var hasForbidden = (bool)task.GetType().GetProperty("Result")!.GetValue(task)!;
            if (hasForbidden)
            {
                throw new GraphQLException(ErrorBuilder.New()
                    .SetMessage($"Permission denied: result set contains {Resource} records that violate ABAC policies")
                    .SetCode("FORBIDDEN")
                    .Build());
            }
        });
    }
}
