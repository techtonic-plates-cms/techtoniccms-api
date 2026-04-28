using System.Reflection;

using HotChocolate;
using HotChocolate.Types;
using HotChocolate.Types.Descriptors;

using TechtonicCmsApi.Schema.TechtonicCms.Enums;
using TechtonicCmsApi.Services;

namespace TechtonicCmsApi.Security;

[AttributeUsage(AttributeTargets.Method)]
public class AbacRequirePermissionAttribute : ObjectFieldDescriptorAttribute
{
    public BaseResource Resource { get; }
    public PermissionAction Action { get; }

    public AbacRequirePermissionAttribute(BaseResource resource, PermissionAction action)
    {
        Resource = resource;
        Action = action;
    }

    protected override void OnConfigure(IDescriptorContext context, IObjectFieldDescriptor descriptor, MemberInfo member)
    {
        descriptor.Use(next => async ctx =>
        {
            var abacService = ctx.Services.GetRequiredService<AbacService>();
            var httpContextAccessor = ctx.Services.GetRequiredService<IHttpContextAccessor>();

            var userIdStr = httpContextAccessor.HttpContext?.User.FindFirst("userId")?.Value;
            if (userIdStr is null || !Guid.TryParse(userIdStr, out var userId))
                throw new GraphQLException(ErrorBuilder.New()
                    .SetMessage("Authentication required")
                    .SetCode("UNAUTHENTICATED")
                    .Build());

            await abacService.RequirePermissionAsync(userId, Resource, Action);

            await next(ctx);
        });
    }
}
