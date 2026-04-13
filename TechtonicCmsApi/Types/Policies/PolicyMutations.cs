using HotChocolate;
using HotChocolate.Authorization;
using HotChocolate.Types;

using Microsoft.EntityFrameworkCore;
using TechtonicCmsApi.Contexts;
using TechtonicCmsApi.Schema.TechtonicCms;
using TechtonicCmsApi.Schema.TechtonicCms.Entities;
using TechtonicCmsApi.Schema.TechtonicCms.Enums;

using PolicyEntity = TechtonicCmsApi.Schema.TechtonicCms.Entities.AbacPolicy;

namespace TechtonicCmsApi.Types.Policies;

public class CreatePolicyRuleInput
{
    [GraphQLType<NonNullType<EnumType<AttributePath>>>]
    public AttributePath AttributePath { get; set; }

    [GraphQLType<NonNullType<EnumType<OperatorType>>>]
    public OperatorType Operator { get; set; }

    [GraphQLType<NonNullType<StringType>>]
    public string ExpectedValue { get; set; } = "";

    [GraphQLType<NonNullType<EnumType<Schema.TechtonicCms.Enums.ValueType>>>]
    public Schema.TechtonicCms.Enums.ValueType ValueType { get; set; }

    public string? Description { get; set; }

    public bool? IsActive { get; set; }

    public int? Order { get; set; }
}

public class CreatePolicyInput
{
    [GraphQLType<NonNullType<StringType>>]
    public string Name { get; set; } = "";

    public string? Description { get; set; }

    [GraphQLType<NonNullType<EnumType<PermissionEffect>>>]
    public PermissionEffect Effect { get; set; }

    [GraphQLType<NonNullType<EnumType<BaseResource>>>]
    public BaseResource ResourceType { get; set; }

    [GraphQLType<NonNullType<EnumType<PermissionAction>>>]
    public PermissionAction ActionType { get; set; }

    public int? Priority { get; set; }

    public bool? IsActive { get; set; }

    [GraphQLType<NonNullType<EnumType<LogicalOperator>>>]
    public LogicalOperator RuleConnector { get; set; }

    public CreatePolicyRuleInput[]? Rules { get; set; }
}

public class UpdatePolicyRuleInput
{
    public Guid? Id { get; set; }

    [GraphQLType<NonNullType<EnumType<AttributePath>>>]
    public AttributePath AttributePath { get; set; }

    [GraphQLType<NonNullType<EnumType<OperatorType>>>]
    public OperatorType Operator { get; set; }

    [GraphQLType<NonNullType<StringType>>]
    public string ExpectedValue { get; set; } = "";

    [GraphQLType<NonNullType<EnumType<Schema.TechtonicCms.Enums.ValueType>>>]
    public Schema.TechtonicCms.Enums.ValueType ValueType { get; set; }

    public string? Description { get; set; }

    public bool? IsActive { get; set; }

    public int? Order { get; set; }
}

public class UpdatePolicyInput
{
    [GraphQLType<NonNullType<IdType>>]
    public Guid Id { get; set; }

    public string? Name { get; set; }

    public string? Description { get; set; }

    public PermissionEffect? Effect { get; set; }

    public int? Priority { get; set; }

    public bool? IsActive { get; set; }

    public LogicalOperator? RuleConnector { get; set; }

    public UpdatePolicyRuleInput[]? Rules { get; set; }

    public Guid[]? DeleteRuleIds { get; set; }
}

public class AssignPolicyToUserInput
{
    [GraphQLType<NonNullType<IdType>>]
    public Guid UserId { get; set; }

    [GraphQLType<NonNullType<IdType>>]
    public Guid PolicyId { get; set; }

    public string? ExpiresAt { get; set; }

    public string? Reason { get; set; }
}

public class PolicyMutation
{
    [Authorize(Policy = "Users:Create")]
    public async Task<PolicyEntity> Create(
        CreatePolicyInput input,
        [Service] TechtonicCmsDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var currentUserId = GetUserId(httpContextAccessor);

        var now = DateTime.UtcNow;
        var policy = new PolicyEntity
        {
            Id = Guid.NewGuid(),
            Name = input.Name,
            Description = input.Description,
            Effect = input.Effect,
            ResourceType = input.ResourceType,
            ActionType = input.ActionType,
            Priority = input.Priority ?? 100,
            IsActive = input.IsActive ?? true,
            RuleConnector = input.RuleConnector,
            CreatedBy = currentUserId,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.AbacPolicies.Add(policy);
        await db.SaveChangesAsync();

        if (input.Rules is { Length: > 0 })
        {
            for (var i = 0; i < input.Rules.Length; i++)
            {
                var ruleInput = input.Rules[i];

                var rule = new AbacPolicyRule
                {
                    Id = Guid.NewGuid(),
                    PolicyId = policy.Id,
                    AttributePath = ruleInput.AttributePath,
                    Operator = ruleInput.Operator,
                    ExpectedValue = ruleInput.ExpectedValue,
                    ValueType = ruleInput.ValueType,
                    Description = ruleInput.Description,
                    IsActive = ruleInput.IsActive ?? true,
                    Order = ruleInput.Order ?? i,
                    CreatedAt = now
                };

                db.AbacPolicyRules.Add(rule);
            }

            await db.SaveChangesAsync();
        }

        return policy;
    }

    [Authorize(Policy = "Users:Update")]
    public async Task<PolicyEntity> Update(
        UpdatePolicyInput input,
        [Service] TechtonicCmsDbContext db)
    {
        var policy = await db.AbacPolicies.FindAsync(input.Id);
        if (policy is null)
        {
            throw new GraphQLException(ErrorBuilder.New()
                .SetMessage("Policy not found")
                .SetCode("NOT_FOUND")
                .Build());
        }

        if (input.Name is not null)
        {
            policy.Name = input.Name;
        }

        if (input.Description is not null)
        {
            policy.Description = input.Description;
        }

        if (input.Effect.HasValue)
        {
            policy.Effect = input.Effect.Value;
        }

        if (input.Priority.HasValue)
        {
            policy.Priority = input.Priority.Value;
        }

        if (input.IsActive.HasValue)
        {
            policy.IsActive = input.IsActive.Value;
        }

        if (input.RuleConnector.HasValue)
        {
            policy.RuleConnector = input.RuleConnector.Value;
        }

        policy.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        if (input.DeleteRuleIds is { Length: > 0 })
        {
            var rulesToDelete = await db.AbacPolicyRules
                .Where(r => input.DeleteRuleIds.Contains(r.Id) && r.PolicyId == policy.Id)
                .ToListAsync();

            db.AbacPolicyRules.RemoveRange(rulesToDelete);
            await db.SaveChangesAsync();
        }

        if (input.Rules is { Length: > 0 })
        {
            foreach (var ruleInput in input.Rules)
            {
                if (ruleInput.Id.HasValue)
                {
                    var existingRule = await db.AbacPolicyRules
                        .FirstOrDefaultAsync(r => r.Id == ruleInput.Id.Value && r.PolicyId == policy.Id);

                    if (existingRule is not null)
                    {
                        existingRule.AttributePath = ruleInput.AttributePath;
                        existingRule.Operator = ruleInput.Operator;
                        existingRule.ExpectedValue = ruleInput.ExpectedValue;
                        existingRule.ValueType = ruleInput.ValueType;
                        existingRule.Description = ruleInput.Description;
                        existingRule.IsActive = ruleInput.IsActive ?? true;
                        existingRule.Order = ruleInput.Order ?? 0;
                    }
                }
                else
                {
                    var newRule = new AbacPolicyRule
                    {
                        Id = Guid.NewGuid(),
                        PolicyId = policy.Id,
                        AttributePath = ruleInput.AttributePath,
                        Operator = ruleInput.Operator,
                        ExpectedValue = ruleInput.ExpectedValue,
                        ValueType = ruleInput.ValueType,
                        Description = ruleInput.Description,
                        IsActive = ruleInput.IsActive ?? true,
                        Order = ruleInput.Order ?? 0,
                        CreatedAt = DateTime.UtcNow
                    };

                    db.AbacPolicyRules.Add(newRule);
                }
            }

            await db.SaveChangesAsync();
        }

        return policy;
    }

    [Authorize(Policy = "Users:Delete")]
    public async Task<bool> Delete(
        Guid id,
        [Service] TechtonicCmsDbContext db)
    {
        var policy = await db.AbacPolicies.FindAsync(id);
        if (policy is not null)
        {
            db.AbacPolicies.Remove(policy);
            await db.SaveChangesAsync();
        }

        return true;
    }

    [Authorize(Policy = "Users:Update")]
    public async Task<bool> AssignToUser(
        AssignPolicyToUserInput input,
        [Service] TechtonicCmsDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var currentUserId = GetUserId(httpContextAccessor);

        var user = await db.Users.FindAsync(input.UserId);
        if (user is null)
        {
            throw new GraphQLException(ErrorBuilder.New()
                .SetMessage("User not found")
                .SetCode("NOT_FOUND")
                .Build());
        }

        var policy = await db.AbacPolicies.FindAsync(input.PolicyId);
        if (policy is null)
        {
            throw new GraphQLException(ErrorBuilder.New()
                .SetMessage("Policy not found")
                .SetCode("NOT_FOUND")
                .Build());
        }

        var existing = await db.UserPolicies
            .AnyAsync(up => up.UserId == input.UserId && up.PolicyId == input.PolicyId);

        if (existing)
        {
            throw new GraphQLException(ErrorBuilder.New()
                .SetMessage("Policy already assigned to user")
                .SetCode("CONFLICT")
                .Build());
        }

        var userPolicy = new UserPolicy
        {
            Id = Guid.NewGuid(),
            UserId = input.UserId,
            PolicyId = input.PolicyId,
            AssignedBy = currentUserId,
            AssignedAt = DateTime.UtcNow,
            ExpiresAt = input.ExpiresAt is not null
                ? DateTime.Parse(input.ExpiresAt, null, System.Globalization.DateTimeStyles.RoundtripKind)
                : null,
            Reason = input.Reason
        };

        db.UserPolicies.Add(userPolicy);
        await db.SaveChangesAsync();

        return true;
    }

    [Authorize(Policy = "Users:Update")]
    public async Task<bool> UnassignFromUser(
        Guid userId,
        Guid policyId,
        [Service] TechtonicCmsDbContext db)
    {
        var userPolicy = await db.UserPolicies
            .FirstOrDefaultAsync(up => up.UserId == userId && up.PolicyId == policyId);

        if (userPolicy is not null)
        {
            db.UserPolicies.Remove(userPolicy);
            await db.SaveChangesAsync();
        }

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
public static class PolicyMutations
{
    public static PolicyMutation Policy() => new();
}
