using HotChocolate;
using HotChocolate.Types;

using Microsoft.EntityFrameworkCore;
using TechtonicCmsApi.Contexts;
using TechtonicCmsApi.Schema.TechtonicCms.Entities;
using TechtonicCmsApi.Schema.TechtonicCms.Enums;

namespace TechtonicCmsApi.Types.Users;

public class RoleRefDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public DateTime? AssignedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

public partial class RoleRefType : ObjectType<RoleRefDto>
{
    protected override void Configure(IObjectTypeDescriptor<RoleRefDto> descriptor)
    {
        descriptor.BindFieldsExplicitly();

        descriptor.Name("RoleRef");

        descriptor.Field(r => r.Id).ID().IsProjected();
        descriptor.Field(r => r.Name).Type<NonNullType<StringType>>().IsProjected();
        descriptor.Field(r => r.Description).IsProjected();
        descriptor.Field(r => r.AssignedAt).IsProjected();
        descriptor.Field(r => r.ExpiresAt).IsProjected();
    }
}

public partial class UserType : ObjectType<User>
{
    protected override void Configure(IObjectTypeDescriptor<User> descriptor)
    {
        descriptor.BindFieldsExplicitly();

        descriptor.Name("User");

        descriptor.Field(u => u.Id).ID().IsProjected();
        descriptor.Field(u => u.Name).Type<NonNullType<StringType>>().IsProjected();
        descriptor.Field(u => u.Status).IsProjected();
        descriptor.Field(u => u.CreationTime).IsProjected();
        descriptor.Field(u => u.LastLoginTime).IsProjected();
        descriptor.Field(u => u.LastEditTime).IsProjected();
    }

    [ExtendObjectType(typeof(UserType))]
    public class UserTypeResolvers
    {
        [UseProjection]
        public IQueryable<RoleRefDto> GetRoles(
        [Parent] User user,
        [Service] TechtonicCmsDbContext db)
        {
            return db.UserRoles
                .Include(ur => ur.Role)
                .Where(ur => ur.UserId == user.Id)
                .Select(ur => new RoleRefDto
                {
                    Id = ur.Role.Id,
                    Name = ur.Role.Name,
                    Description = ur.Role.Description,
                    AssignedAt = ur.AssignedAt,
                    ExpiresAt = ur.ExpiresAt
                });
        }
    }
}
