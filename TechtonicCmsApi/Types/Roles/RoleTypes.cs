using HotChocolate;
using HotChocolate.Types;

using Microsoft.EntityFrameworkCore;
using TechtonicCmsApi.Contexts;
using TechtonicCmsApi.Schema.TechtonicCms;
using TechtonicCmsApi.Schema.TechtonicCms.Entities;
using TechtonicCmsApi.Schema.TechtonicCms.Enums;

namespace TechtonicCmsApi.Types.Roles;

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
        [UseProjection]
        public IQueryable<UserRole> GetUsers(
            [Parent] Role role,
            [Service] TechtonicCmsDbContext db)
        {
            return db.UserRoles
                .Include(ur => ur.User)
                .Where(ur => ur.RoleId == role.Id);
        }

        [UseProjection]
        public IQueryable<RolePolicy> GetPolicies(
            [Parent] Role role,
            [Service] TechtonicCmsDbContext db)
        {
            return db.RolePolicies
                .Include(rp => rp.Policy)
                .Where(rp => rp.RoleId == role.Id);
        }
    }
}
