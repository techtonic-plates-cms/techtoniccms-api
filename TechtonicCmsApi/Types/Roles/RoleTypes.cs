using HotChocolate;
using HotChocolate.Types;

using Microsoft.EntityFrameworkCore;
using TechtonicCmsApi.Contexts;
using TechtonicCmsApi.Schema.TechtonicCms;
using TechtonicCmsApi.Schema.TechtonicCms.Entities;
using TechtonicCmsApi.Schema.TechtonicCms.Enums;

namespace TechtonicCmsApi.Types.Roles;

public class UserRefInRoleDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public UserStatus Status { get; set; }
    public DateTime? AssignedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

public class PolicyRefInRoleDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public PermissionEffect Effect { get; set; }
    public BaseResource ResourceType { get; set; }
    public PermissionAction ActionType { get; set; }
    public DateTime? AssignedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

[ObjectType<UserRefInRoleDto>]
public static partial class UserRefInRoleType
{
    public static string GetId([Parent] UserRefInRoleDto user) => user.Id.ToString();

    [GraphQLType<NonNullType<StringType>>]
    public static string GetName([Parent] UserRefInRoleDto user) => user.Name;

    public static UserStatus GetStatus([Parent] UserRefInRoleDto user) => user.Status;

    public static string? GetAssignedAt([Parent] UserRefInRoleDto user) =>
        user.AssignedAt?.ToUniversalTime().ToString("o");

    public static string? GetExpiresAt([Parent] UserRefInRoleDto user) =>
        user.ExpiresAt?.ToUniversalTime().ToString("o");
}

[ObjectType<PolicyRefInRoleDto>]
public static partial class PolicyRefInRoleType
{
    public static string GetId([Parent] PolicyRefInRoleDto policy) => policy.Id.ToString();

    [GraphQLType<NonNullType<StringType>>]
    public static string GetName([Parent] PolicyRefInRoleDto policy) => policy.Name;

    public static string? GetDescription([Parent] PolicyRefInRoleDto policy) => policy.Description;

    public static PermissionEffect GetEffect([Parent] PolicyRefInRoleDto policy) => policy.Effect;

    public static BaseResource GetResourceType([Parent] PolicyRefInRoleDto policy) => policy.ResourceType;

    public static PermissionAction GetActionType([Parent] PolicyRefInRoleDto policy) => policy.ActionType;

    public static string? GetAssignedAt([Parent] PolicyRefInRoleDto policy) =>
        policy.AssignedAt?.ToUniversalTime().ToString("o");

    public static string? GetExpiresAt([Parent] PolicyRefInRoleDto policy) =>
        policy.ExpiresAt?.ToUniversalTime().ToString("o");
}

[ObjectType<Role>]
public static partial class RoleType
{
    public static string GetId([Parent] Role role) => role.Id.ToString();

    [GraphQLType<NonNullType<StringType>>]
    public static string GetName([Parent] Role role) => role.Name;

    public static string? GetDescription([Parent] Role role) => role.Description;

    public static string? GetCreationTime([Parent] Role role) =>
        role.CreationTime.ToUniversalTime().ToString("o");

    public static string? GetLastEditTime([Parent] Role role) =>
        role.LastEditTime.ToUniversalTime().ToString("o");

    public static async Task<IEnumerable<UserRefInRoleDto>> GetUsers(
        [Parent] Role role,
        [Service] TechtonicCmsDbContext db)
    {
        var roleUsers = await db.UserRoles
            .Include(ur => ur.User)
            .Where(ur => ur.RoleId == role.Id)
            .Select(ur => new UserRefInRoleDto
            {
                Id = ur.User.Id,
                Name = ur.User.Name,
                Status = ur.User.Status,
                AssignedAt = ur.AssignedAt,
                ExpiresAt = ur.ExpiresAt
            })
            .ToListAsync();

        return roleUsers;
    }

    public static async Task<IEnumerable<PolicyRefInRoleDto>> GetPolicies(
        [Parent] Role role,
        [Service] TechtonicCmsDbContext db)
    {
        var rolePolicies = await db.RolePolicies
            .Include(rp => rp.Policy)
            .Where(rp => rp.RoleId == role.Id)
            .Select(rp => new PolicyRefInRoleDto
            {
                Id = rp.Policy.Id,
                Name = rp.Policy.Name,
                Description = rp.Policy.Description,
                Effect = rp.Policy.Effect,
                ResourceType = rp.Policy.ResourceType,
                ActionType = rp.Policy.ActionType,
                AssignedAt = rp.AssignedAt,
                ExpiresAt = rp.ExpiresAt
            })
            .ToListAsync();

        return rolePolicies;
    }
}
