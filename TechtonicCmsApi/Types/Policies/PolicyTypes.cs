using HotChocolate;
using HotChocolate.Types;

using Microsoft.EntityFrameworkCore;
using TechtonicCmsApi.Contexts;
using TechtonicCmsApi.Schema.TechtonicCms;
using TechtonicCmsApi.Schema.TechtonicCms.Entities;
using TechtonicCmsApi.Schema.TechtonicCms.Enums;

namespace TechtonicCmsApi.Types.Policies;

public class RoleAssignmentDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public DateTime? AssignedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string? Reason { get; set; }
}

public class UserAssignmentDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public DateTime? AssignedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string? Reason { get; set; }
}

[ObjectType<RoleAssignmentDto>]
public static partial class RoleAssignmentType
{
    public static string GetId([Parent] RoleAssignmentDto assignment) => assignment.Id.ToString();

    [GraphQLType<NonNullType<StringType>>]
    public static string GetName([Parent] RoleAssignmentDto assignment) => assignment.Name;

    public static string? GetAssignedAt([Parent] RoleAssignmentDto assignment) =>
        assignment.AssignedAt?.ToUniversalTime().ToString("o");

    public static string? GetExpiresAt([Parent] RoleAssignmentDto assignment) =>
        assignment.ExpiresAt?.ToUniversalTime().ToString("o");

    public static string? GetReason([Parent] RoleAssignmentDto assignment) => assignment.Reason;
}

[ObjectType<UserAssignmentDto>]
public static partial class UserAssignmentType
{
    public static string GetId([Parent] UserAssignmentDto assignment) => assignment.Id.ToString();

    [GraphQLType<NonNullType<StringType>>]
    public static string GetName([Parent] UserAssignmentDto assignment) => assignment.Name;

    public static string? GetAssignedAt([Parent] UserAssignmentDto assignment) =>
        assignment.AssignedAt?.ToUniversalTime().ToString("o");

    public static string? GetExpiresAt([Parent] UserAssignmentDto assignment) =>
        assignment.ExpiresAt?.ToUniversalTime().ToString("o");

    public static string? GetReason([Parent] UserAssignmentDto assignment) => assignment.Reason;
}

[ObjectType<AbacPolicyRule>]
public static partial class PolicyRuleType
{
    public static string GetId([Parent] AbacPolicyRule rule) => rule.Id.ToString();

    public static string GetPolicyId([Parent] AbacPolicyRule rule) => rule.PolicyId.ToString();

    [GraphQLType<NonNullType<StringType>>]
    public static string GetAttributePath([Parent] AbacPolicyRule rule) => rule.AttributePath.ToString();

    [GraphQLType<NonNullType<StringType>>]
    public static string GetOperator([Parent] AbacPolicyRule rule) => rule.Operator.ToString();

    [GraphQLType<NonNullType<StringType>>]
    public static string GetExpectedValue([Parent] AbacPolicyRule rule) => rule.ExpectedValue;

    [GraphQLType<NonNullType<StringType>>]
    public static string GetValueType([Parent] AbacPolicyRule rule) => rule.ValueType.ToString();

    public static string? GetDescription([Parent] AbacPolicyRule rule) => rule.Description;

    public static bool GetIsActive([Parent] AbacPolicyRule rule) => rule.IsActive;

    public static int GetOrder([Parent] AbacPolicyRule rule) => rule.Order;

    public static string? GetCreatedAt([Parent] AbacPolicyRule rule) =>
        rule.CreatedAt.ToUniversalTime().ToString("o");
}

[ObjectType<AbacPolicy>]
public static partial class PolicyType
{
    public static string GetId([Parent] AbacPolicy policy) => policy.Id.ToString();

    [GraphQLType<NonNullType<StringType>>]
    public static string GetName([Parent] AbacPolicy policy) => policy.Name;

    public static string? GetDescription([Parent] AbacPolicy policy) => policy.Description;

    [GraphQLType<NonNullType<StringType>>]
    public static string GetEffect([Parent] AbacPolicy policy) => policy.Effect.ToString();

    public static int GetPriority([Parent] AbacPolicy policy) => policy.Priority;

    public static bool GetIsActive([Parent] AbacPolicy policy) => policy.IsActive;

    [GraphQLType<NonNullType<StringType>>]
    public static string GetResourceType([Parent] AbacPolicy policy) => policy.ResourceType.ToString();

    [GraphQLType<NonNullType<StringType>>]
    public static string GetActionType([Parent] AbacPolicy policy) => policy.ActionType.ToString();

    [GraphQLType<NonNullType<StringType>>]
    public static string GetRuleConnector([Parent] AbacPolicy policy) => policy.RuleConnector.ToString();

    public static string? GetCreatedBy([Parent] AbacPolicy policy) => policy.CreatedBy.ToString();

    public static string? GetCreatedAt([Parent] AbacPolicy policy) =>
        policy.CreatedAt.ToUniversalTime().ToString("o");

    public static string? GetUpdatedAt([Parent] AbacPolicy policy) =>
        policy.UpdatedAt.ToUniversalTime().ToString("o");

    public static string? GetLastEvaluatedAt([Parent] AbacPolicy policy) =>
        policy.LastEvaluatedAt?.ToUniversalTime().ToString("o");

    public static async Task<IEnumerable<AbacPolicyRule>> GetRules(
        [Parent] AbacPolicy policy,
        [Service] TechtonicCmsDbContext db)
    {
        return await db.AbacPolicyRules
            .Where(r => r.PolicyId == policy.Id)
            .OrderBy(r => r.Order)
            .ToListAsync();
    }

    public static async Task<IEnumerable<RoleAssignmentDto>> GetAssignedToRoles(
        [Parent] AbacPolicy policy,
        [Service] TechtonicCmsDbContext db)
    {
        var assignments = await db.RolePolicies
            .Include(rp => rp.Role)
            .Where(rp => rp.PolicyId == policy.Id)
            .Select(rp => new RoleAssignmentDto
            {
                Id = rp.Role.Id,
                Name = rp.Role.Name,
                AssignedAt = rp.AssignedAt,
                ExpiresAt = rp.ExpiresAt,
                Reason = rp.Reason
            })
            .ToListAsync();

        return assignments;
    }

    public static async Task<IEnumerable<UserAssignmentDto>> GetAssignedToUsers(
        [Parent] AbacPolicy policy,
        [Service] TechtonicCmsDbContext db)
    {
        var assignments = await db.UserPolicies
            .Include(up => up.User)
            .Where(up => up.PolicyId == policy.Id)
            .Select(up => new UserAssignmentDto
            {
                Id = up.User.Id,
                Name = up.User.Name,
                AssignedAt = up.AssignedAt,
                ExpiresAt = up.ExpiresAt,
                Reason = up.Reason
            })
            .ToListAsync();

        return assignments;
    }
}
