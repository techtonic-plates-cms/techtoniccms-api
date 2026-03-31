using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

using TechtonicCmsApi.Schema.TechtonicCms.Enums;
using TechtonicCmsApi.Services;

namespace TechtonicCmsApi.Security;

public class AbacRequirement : IAuthorizationRequirement
{
    public BaseResource Resource { get; }
    public PermissionAction Action { get; }

    public AbacRequirement(BaseResource resource, PermissionAction action)
    {
        Resource = resource;
        Action = action;
    }
}

public class AbacAuthorizationHandler : AuthorizationHandler<AbacRequirement>
{
    private readonly AbacService _abacService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AbacAuthorizationHandler(AbacService abacService, IHttpContextAccessor httpContextAccessor)
    {
        _abacService = abacService;
        _httpContextAccessor = httpContextAccessor;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AbacRequirement requirement)
    {
        var userIdClaim = context.User.FindFirst("userId")
            ?? context.User.FindFirst(ClaimTypes.NameIdentifier)
            ?? context.User.FindFirst("sub");

        if (userIdClaim is null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            context.Fail();
            return;
        }

        var allowed = await _abacService.CheckPermissionAsync(
            userId,
            requirement.Resource,
            requirement.Action,
            null);

        if (allowed)
        {
            context.Succeed(requirement);
        }
        else
        {
            context.Fail();
        }
    }
}

public static class SecurityPolicies
{
    public static void Register(AuthorizationOptions options)
    {
        foreach (BaseResource resource in Enum.GetValues<BaseResource>())
        {
            foreach (PermissionAction action in Enum.GetValues<PermissionAction>())
            {
                var policyName = $"{resource}:{action}";
                options.AddPolicy(policyName, policy =>
                    policy.Requirements.Add(new AbacRequirement(resource, action)));
            }
        }
    }
}
