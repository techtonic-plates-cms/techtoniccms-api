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

public partial class RoleAssignmentType : ObjectType<RoleAssignmentDto>
{
    protected override void Configure(IObjectTypeDescriptor<RoleAssignmentDto> descriptor)
    {
        descriptor.BindFieldsExplicitly();

        descriptor.Name("RoleAssignment");

        descriptor.Field(a => a.Id).ID().IsProjected();
        descriptor.Field(a => a.Name).Type<NonNullType<StringType>>().IsProjected();
        descriptor.Field(a => a.AssignedAt).IsProjected();
        descriptor.Field(a => a.ExpiresAt).IsProjected();
        descriptor.Field(a => a.Reason).IsProjected();
    }
}

public partial class UserAssignmentType : ObjectType<UserAssignmentDto>
{
    protected override void Configure(IObjectTypeDescriptor<UserAssignmentDto> descriptor)
    {
        descriptor.BindFieldsExplicitly();

        descriptor.Name("UserAssignment");

        descriptor.Field(a => a.Id).ID().IsProjected();
        descriptor.Field(a => a.Name).Type<NonNullType<StringType>>().IsProjected();
        descriptor.Field(a => a.AssignedAt).IsProjected();
        descriptor.Field(a => a.ExpiresAt).IsProjected();
        descriptor.Field(a => a.Reason).IsProjected();
    }
}

public partial class PolicyRuleType : ObjectType<AbacPolicyRule>
{
    protected override void Configure(IObjectTypeDescriptor<AbacPolicyRule> descriptor)
    {
        descriptor.BindFieldsExplicitly();

        descriptor.Name("PolicyRule");

        descriptor.Field(r => r.Id).ID().IsProjected();
        descriptor.Field(r => r.PolicyId).ID().IsProjected();
        descriptor.Field(r => r.AttributePath).IsProjected();
        descriptor.Field(r => r.Operator).IsProjected();
        descriptor.Field(r => r.ExpectedValue).Type<NonNullType<StringType>>().IsProjected();
        descriptor.Field(r => r.ValueType).IsProjected();
        descriptor.Field(r => r.Description).IsProjected();
        descriptor.Field(r => r.IsActive).IsProjected();
        descriptor.Field(r => r.Order).IsProjected();
        descriptor.Field(r => r.CreatedAt).IsProjected();
    }
}

public partial class PolicyType : ObjectType<AbacPolicy>
{
    protected override void Configure(IObjectTypeDescriptor<AbacPolicy> descriptor)
    {
        descriptor.BindFieldsExplicitly();

        descriptor.Name("Policy");

        descriptor.Field(p => p.Id).ID().IsProjected();
        descriptor.Field(p => p.Name).Type<NonNullType<StringType>>().IsProjected();
        descriptor.Field(p => p.Description).IsProjected();
        descriptor.Field(p => p.Effect).IsProjected();
        descriptor.Field(p => p.Priority).IsProjected();
        descriptor.Field(p => p.IsActive).IsProjected();
        descriptor.Field(p => p.ResourceType).IsProjected();
        descriptor.Field(p => p.ActionType).IsProjected();
        descriptor.Field(p => p.RuleConnector).IsProjected();
        descriptor.Field(p => p.CreatedBy).ID().IsProjected();
        descriptor.Field(p => p.CreatedAt).IsProjected();
        descriptor.Field(p => p.UpdatedAt).IsProjected();
        descriptor.Field(p => p.LastEvaluatedAt).IsProjected();
    }

    [ExtendObjectType(typeof(PolicyType))]
    public class PolicyTypeResolvers
    {
        public async Task<IEnumerable<AbacPolicyRule>> GetRules(
            [Parent] AbacPolicy policy,
            [Service] TechtonicCmsDbContext db)
        {
            return await db.AbacPolicyRules
                .Where(r => r.PolicyId == policy.Id)
                .OrderBy(r => r.Order)
                .ToListAsync();
        }

        public async Task<IEnumerable<RoleAssignmentDto>> GetAssignedToRoles(
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

        public async Task<IEnumerable<UserAssignmentDto>> GetAssignedToUsers(
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
}
