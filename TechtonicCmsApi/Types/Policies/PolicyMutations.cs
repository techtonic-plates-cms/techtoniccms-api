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

[OneOf]
public class PolicyRuleValueInput
{
    public string? StringValue { get; set; }
    public double? NumberValue { get; set; }
    public bool? BooleanValue { get; set; }
    public Guid? UuidValue { get; set; }
    public DateTime? DateTimeValue { get; set; }
    public string[]? ArrayValue { get; set; }
    public AttributePath? ContextReferencePath { get; set; }
}

public class CreatePolicyRuleInput
{
    [GraphQLType<NonNullType<EnumType<AttributePath>>>]
    public AttributePath AttributePath { get; set; }

    [GraphQLType<NonNullType<EnumType<OperatorType>>>]
    public OperatorType Operator { get; set; }

    public PolicyRuleValueInput? Value { get; set; }

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

    public PolicyRuleValueInput? Value { get; set; }

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

public class PolicyMutation
{
    [Authorize(Policy = "Policies:Create")]
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
                    Description = ruleInput.Description,
                    IsActive = ruleInput.IsActive ?? true,
                    Order = ruleInput.Order ?? i,
                    CreatedAt = now
                };

                ApplyRuleValue(rule, ruleInput.Value, ruleInput.Operator);
                db.AbacPolicyRules.Add(rule);
            }

            await db.SaveChangesAsync();
        }

        return policy;
    }

    [Authorize(Policy = "Policies:Update")]
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
                        existingRule.Description = ruleInput.Description;
                        existingRule.IsActive = ruleInput.IsActive ?? true;
                        existingRule.Order = ruleInput.Order ?? 0;
                        ApplyRuleValue(existingRule, ruleInput.Value, ruleInput.Operator);
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
                        Description = ruleInput.Description,
                        IsActive = ruleInput.IsActive ?? true,
                        Order = ruleInput.Order ?? 0,
                        CreatedAt = DateTime.UtcNow
                    };

                    ApplyRuleValue(newRule, ruleInput.Value, ruleInput.Operator);
                    db.AbacPolicyRules.Add(newRule);
                }
            }

            await db.SaveChangesAsync();
        }

        return policy;
    }

    [Authorize(Policy = "Policies:Delete")]
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

    private static void ApplyRuleValue(AbacPolicyRule rule, PolicyRuleValueInput? value, OperatorType op)
    {
        rule.ExpectedStringValue = null;
        rule.ExpectedNumberValue = null;
        rule.ExpectedBooleanValue = null;
        rule.ExpectedUuidValue = null;
        rule.ExpectedDateTimeValue = null;
        rule.ExpectedArrayValue = null;
        rule.ContextReferencePath = null;

        if (op == OperatorType.IsNull || op == OperatorType.IsNotNull)
        {
            rule.ValueType = Schema.TechtonicCms.Enums.ValueType.String;
            return;
        }

        if (value is null)
        {
            throw new GraphQLException(ErrorBuilder.New()
                .SetMessage("Value is required for this operator")
                .SetCode("BAD_REQUEST")
                .Build());
        }

        var (valueType, typedValue, contextRef) = ResolveRuleValue(value);

        if (op == OperatorType.EqContextRef && contextRef is null)
        {
            throw new GraphQLException(ErrorBuilder.New()
                .SetMessage("Operator EqContextRef requires ContextReferencePath")
                .SetCode("BAD_REQUEST")
                .Build());
        }

        if ((op == OperatorType.In || op == OperatorType.NotIn) && typedValue is not string[])
        {
            throw new GraphQLException(ErrorBuilder.New()
                .SetMessage("Operator In/NotIn requires ArrayValue")
                .SetCode("BAD_REQUEST")
                .Build());
        }

        if (op != OperatorType.EqContextRef && op != OperatorType.In && op != OperatorType.NotIn && typedValue is string[])
        {
            throw new GraphQLException(ErrorBuilder.New()
                .SetMessage("ArrayValue is only valid with In/NotIn operators")
                .SetCode("BAD_REQUEST")
                .Build());
        }

        switch (valueType)
        {
            case Schema.TechtonicCms.Enums.ValueType.String:
                rule.ExpectedStringValue = (string?)typedValue;
                break;
            case Schema.TechtonicCms.Enums.ValueType.Number:
                rule.ExpectedNumberValue = (double?)typedValue;
                break;
            case Schema.TechtonicCms.Enums.ValueType.Boolean:
                rule.ExpectedBooleanValue = (bool?)typedValue;
                break;
            case Schema.TechtonicCms.Enums.ValueType.Uuid:
                rule.ExpectedUuidValue = (Guid?)typedValue;
                break;
            case Schema.TechtonicCms.Enums.ValueType.Datetime:
                rule.ExpectedDateTimeValue = (DateTime?)typedValue;
                break;
            case Schema.TechtonicCms.Enums.ValueType.Array:
                rule.ExpectedArrayValue = (string[]?)typedValue;
                break;
        }

        rule.ContextReferencePath = contextRef;
        rule.ValueType = valueType;
    }

    private static (Schema.TechtonicCms.Enums.ValueType valueType, object? typedValue, AttributePath? contextRef) ResolveRuleValue(PolicyRuleValueInput value)
    {
        int populatedCount = 0;
        Schema.TechtonicCms.Enums.ValueType? derivedType = null;
        object? typedValue = null;
        AttributePath? contextRef = null;

        if (value.StringValue is not null)       { populatedCount++; derivedType = Schema.TechtonicCms.Enums.ValueType.String;  typedValue = value.StringValue; }
        if (value.NumberValue.HasValue)          { populatedCount++; derivedType = Schema.TechtonicCms.Enums.ValueType.Number;  typedValue = value.NumberValue.Value; }
        if (value.BooleanValue.HasValue)         { populatedCount++; derivedType = Schema.TechtonicCms.Enums.ValueType.Boolean; typedValue = value.BooleanValue.Value; }
        if (value.UuidValue.HasValue)            { populatedCount++; derivedType = Schema.TechtonicCms.Enums.ValueType.Uuid;    typedValue = value.UuidValue.Value; }
        if (value.DateTimeValue.HasValue)        { populatedCount++; derivedType = Schema.TechtonicCms.Enums.ValueType.Datetime; typedValue = value.DateTimeValue.Value; }
        if (value.ArrayValue is not null)        { populatedCount++; derivedType = Schema.TechtonicCms.Enums.ValueType.Array;   typedValue = value.ArrayValue; }
        if (value.ContextReferencePath.HasValue) { populatedCount++; derivedType = Schema.TechtonicCms.Enums.ValueType.Uuid;    contextRef = value.ContextReferencePath.Value; }

        if (populatedCount != 1)
            throw new GraphQLException(ErrorBuilder.New()
                .SetMessage("Exactly one value field must be provided in the rule value input")
                .SetCode("BAD_REQUEST").Build());

        return (derivedType!.Value, typedValue, contextRef);
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
