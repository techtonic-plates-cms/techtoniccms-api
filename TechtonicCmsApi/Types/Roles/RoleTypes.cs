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

public partial class UserRefInRoleType : ObjectType<UserRefInRoleDto>
{
    protected override void Configure(IObjectTypeDescriptor<UserRefInRoleDto> descriptor)
    {
        descriptor.BindFieldsExplicitly();

        descriptor.Name("UserRefInRole");

        descriptor.Field(u => u.Id).ID().IsProjected();
        descriptor.Field(u => u.Name).Type<NonNullType<StringType>>().IsProjected();
        descriptor.Field(u => u.Status).IsProjected();
        descriptor.Field(u => u.AssignedAt).IsProjected();
        descriptor.Field(u => u.ExpiresAt).IsProjected();
    }
}

public partial class PolicyRefInRoleType : ObjectType<PolicyRefInRoleDto>
{
    protected override void Configure(IObjectTypeDescriptor<PolicyRefInRoleDto> descriptor)
    {
        descriptor.BindFieldsExplicitly();

        descriptor.Name("PolicyRefInRole");

        descriptor.Field(p => p.Id).ID().IsProjected();
        descriptor.Field(p => p.Name).Type<NonNullType<StringType>>().IsProjected();
        descriptor.Field(p => p.Description).IsProjected();
        descriptor.Field(p => p.Effect).IsProjected();
        descriptor.Field(p => p.ResourceType).IsProjected();
        descriptor.Field(p => p.ActionType).IsProjected();
        descriptor.Field(p => p.AssignedAt).IsProjected();
        descriptor.Field(p => p.ExpiresAt).IsProjected();
    }
}

public partial class RoleType : ObjectType<Role>
{
    protected override void Configure(IObjectTypeDescriptor<Role> descriptor)
    {
        descriptor.BindFieldsExplicitly();

        descriptor.Name("Role");

        descriptor.Field(r => r.Id).ID().IsProjected();
        descriptor.Field(r => r.Name).Type<NonNullType<StringType>>().IsProjected();
        descriptor.Field(r => r.Description).IsProjected();
        descriptor.Field(r => r.CreationTime).IsProjected();
        descriptor.Field(r => r.LastEditTime).IsProjected();
    }

    [ExtendObjectType(typeof(RoleType))]
    public class RoleTypeResolvers
    {
        public async Task<IEnumerable<UserRefInRoleDto>> GetUsers(
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

        public async Task<IEnumerable<PolicyRefInRoleDto>> GetPolicies(
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
}
