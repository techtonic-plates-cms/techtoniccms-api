using HotChocolate;
using HotChocolate.Authorization;
using HotChocolate.Types;

using Microsoft.EntityFrameworkCore;
using TechtonicCmsApi.Contexts;
using TechtonicCmsApi.Schema.TechtonicCms;
using TechtonicCmsApi.Schema.TechtonicCms.Entities;
using TechtonicCmsApi.Schema.TechtonicCms.Enums;
using TechtonicCmsApi.Security;

using RoleEntity = TechtonicCmsApi.Schema.TechtonicCms.Entities.Role;

namespace TechtonicCmsApi.Types.Roles;

public class PolicyAssignmentInRoleInput
{
    [GraphQLType<NonNullType<IdType>>]
    public Guid PolicyId { get; set; }

    public string? ExpiresAt { get; set; }

    public string? Reason { get; set; }
}

[OneOf]
public class PolicyAssignmentInRoleChoiceInput
{
    public Guid[]? Ids { get; set; }

    public PolicyAssignmentInRoleInput[]? Assignments { get; set; }
}

public class CreateRoleInput
{
    [GraphQLType<NonNullType<StringType>>]
    public string Name { get; set; } = "";

    public string? Description { get; set; }

    public PolicyAssignmentInRoleChoiceInput? Policies { get; set; }
}

public class UpdateRoleInput
{
    [GraphQLType<NonNullType<IdType>>]
    public Guid Id { get; set; }

    public string? Name { get; set; }

    public string? Description { get; set; }
}

public class AssignPolicyToRoleInput
{
    [GraphQLType<NonNullType<IdType>>]
    public Guid RoleId { get; set; }

    [GraphQLType<NonNullType<IdType>>]
    public Guid PolicyId { get; set; }

    public string? ExpiresAt { get; set; }

    public string? Reason { get; set; }
}

public class RoleMutation
{
    [Authorize]
    [AbacRequirePermission(BaseResource.Roles, PermissionAction.Create)]
    public async Task<RoleEntity> Create(
        CreateRoleInput input,
        [Service] TechtonicCmsDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var currentUserId = GetUserId(httpContextAccessor);

        var now = DateTime.UtcNow;
        var role = new RoleEntity
        {
            Id = Guid.NewGuid(),
            Name = input.Name,
            Description = input.Description,
            CreationTime = now,
            LastEditTime = now
        };

        db.Roles.Add(role);
        await db.SaveChangesAsync();

        if (input.Policies?.Ids is { Length: > 0 })
        {
            foreach (var policyId in input.Policies.Ids)
            {
                db.RolePolicies.Add(new RolePolicy
                {
                    Id = Guid.NewGuid(),
                    RoleId = role.Id,
                    PolicyId = policyId,
                    AssignedBy = currentUserId,
                    AssignedAt = DateTime.UtcNow
                });
            }

            await db.SaveChangesAsync();
        }

        if (input.Policies?.Assignments is { Length: > 0 })
        {
            foreach (var assignment in input.Policies.Assignments)
            {
                db.RolePolicies.Add(new RolePolicy
                {
                    Id = Guid.NewGuid(),
                    RoleId = role.Id,
                    PolicyId = assignment.PolicyId,
                    AssignedBy = currentUserId,
                    AssignedAt = DateTime.UtcNow,
                    ExpiresAt = assignment.ExpiresAt is not null
                        ? DateTime.Parse(assignment.ExpiresAt, null, System.Globalization.DateTimeStyles.RoundtripKind)
                        : null,
                    Reason = assignment.Reason
                });
            }

            await db.SaveChangesAsync();
        }

        return role;
    }

    [Authorize]
    [AbacRequirePermission(BaseResource.Roles, PermissionAction.Update)]
    public async Task<RoleEntity> Update(
        UpdateRoleInput input,
        [Service] TechtonicCmsDbContext db)
    {
        var role = await db.Roles.FindAsync(input.Id);
        if (role is null)
            throw new GraphQLException(ErrorBuilder.New()
                .SetMessage("Role not found")
                .SetCode("NOT_FOUND")
                .Build());

        if (input.Name is not null)
            role.Name = input.Name;

        if (input.Description is not null)
            role.Description = input.Description;

        role.LastEditTime = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return role;
    }

    [Authorize]
    [AbacRequirePermission(BaseResource.Roles, PermissionAction.Delete)]
    public async Task<bool> Delete(
        Guid id,
        [Service] TechtonicCmsDbContext db)
    {
        var role = await db.Roles.FindAsync(id);
        if (role is not null)
        {
            if (role.Name == "admin")
                throw new GraphQLException(ErrorBuilder.New()
                    .SetMessage("Cannot delete the admin role")
                    .SetCode("FORBIDDEN")
                    .Build());

            db.Roles.Remove(role);
            await db.SaveChangesAsync();
        }

        return true;
    }

    [Authorize]
    [AbacRequirePermission(BaseResource.Roles, PermissionAction.Update)]
    public async Task<bool> AssignPolicy(
        AssignPolicyToRoleInput input,
        [Service] TechtonicCmsDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var currentUserId = GetUserId(httpContextAccessor);

        var role = await db.Roles.FindAsync(input.RoleId);
        if (role is null)
            throw new GraphQLException(ErrorBuilder.New()
                .SetMessage("Role not found")
                .SetCode("NOT_FOUND")
                .Build());

        var policy = await db.AbacPolicies.FindAsync(input.PolicyId);
        if (policy is null)
            throw new GraphQLException(ErrorBuilder.New()
                .SetMessage("Policy not found")
                .SetCode("NOT_FOUND")
                .Build());

        var exists = await db.RolePolicies
            .AnyAsync(rp => rp.RoleId == input.RoleId && rp.PolicyId == input.PolicyId);

        if (exists)
            throw new GraphQLException(ErrorBuilder.New()
                .SetMessage("Policy already assigned to role")
                .SetCode("CONFLICT")
                .Build());

        db.RolePolicies.Add(new RolePolicy
        {
            Id = Guid.NewGuid(),
            RoleId = input.RoleId,
            PolicyId = input.PolicyId,
            AssignedBy = currentUserId,
            AssignedAt = DateTime.UtcNow,
            ExpiresAt = input.ExpiresAt is not null
                ? DateTime.Parse(input.ExpiresAt, null, System.Globalization.DateTimeStyles.RoundtripKind)
                : null,
            Reason = input.Reason
        });

        await db.SaveChangesAsync();

        return true;
    }

    [Authorize]
    [AbacRequirePermission(BaseResource.Roles, PermissionAction.Update)]
    public async Task<bool> UnassignPolicy(
        Guid roleId,
        Guid policyId,
        [Service] TechtonicCmsDbContext db)
    {
        var rolePolicy = await db.RolePolicies
            .FirstOrDefaultAsync(rp => rp.RoleId == roleId && rp.PolicyId == policyId);

        if (rolePolicy is not null)
        {
            db.RolePolicies.Remove(rolePolicy);
            await db.SaveChangesAsync();
        }

        return true;
    }

    [Authorize]
    [AbacRequirePermission(BaseResource.Roles, PermissionAction.Update)]
    public async Task<bool> UpdatePolicyExpiration(
        Guid roleId,
        Guid policyId,
        string? expiresAt,
        string? reason,
        [Service] TechtonicCmsDbContext db)
    {
        var rolePolicy = await db.RolePolicies
            .FirstOrDefaultAsync(rp => rp.RoleId == roleId && rp.PolicyId == policyId);

        if (rolePolicy is null)
            throw new GraphQLException(ErrorBuilder.New()
                .SetMessage("Policy assignment not found")
                .SetCode("NOT_FOUND")
                .Build());

        rolePolicy.ExpiresAt = expiresAt is not null
            ? DateTime.Parse(expiresAt, null, System.Globalization.DateTimeStyles.RoundtripKind)
            : null;

        if (reason is not null)
            rolePolicy.Reason = reason;

        await db.SaveChangesAsync();
        return true;
    }

    private static Guid GetUserId(IHttpContextAccessor httpContextAccessor)
    {
        var userIdStr = httpContextAccessor.HttpContext?.User.FindFirst("userId")?.Value;
        if (userIdStr is null || !Guid.TryParse(userIdStr, out var userId))
            throw new GraphQLException(ErrorBuilder.New()
                .SetMessage("Authentication required")
                .SetCode("UNAUTHENTICATED")
                .Build());

        return userId;
    }
}

[ExtendObjectType(nameof(Mutation))]
public static class RoleMutations
{
    public static RoleMutation Roles() => new();
}
