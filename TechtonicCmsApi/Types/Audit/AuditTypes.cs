using HotChocolate.Authorization;
using TechtonicCmsApi.Contexts;
using TechtonicCmsApi.Schema.TechtonicCms.Entities;

namespace TechtonicCmsApi.Types.Audit;

class AuditType : ObjectType<AbacAudit>
{
    protected override void Configure(IObjectTypeDescriptor<AbacAudit> descriptor)
    {
        descriptor.BindFieldsExplicitly();

        descriptor.Name("Audit");

        descriptor.Field(a => a.Id).ID().IsProjected();
        descriptor.Field(a => a.RequestedAction).IsProjected();
        descriptor.Field(a => a.ResourceType).IsProjected();
        descriptor.Field(a => a.Decision).IsProjected();
        descriptor.Field(a => a.DecisionReason).IsProjected();
        descriptor.Field(a => a.EvaluationTimeMs).IsProjected();
        descriptor.Field(a => a.Timestamp).IsProjected();
        descriptor.Field(a => a.IpAddress).IsProjected();
        descriptor.Field(a => a.UserAgent).IsProjected();
        descriptor.Field(a => a.SessionId).IsProjected();

        descriptor.Field(a => a.User);
        descriptor.Field(a => a.EvaluatedPolicyIds).Type<ListType<NonNullType<UuidType>>>().IsProjected();
        descriptor.Field(a => a.MatchingPolicyIds).Type<ListType<NonNullType<UuidType>>>().IsProjected();
    }


    public class AuditTypeResolvers
    {
        [Authorize(Policy = "Policies:Read")]
        [UseProjection]
        public IQueryable<AbacPolicy> GetEvaluatedPolicies(
            [Parent] AbacAudit audit,
            [Service] TechtonicCmsDbContext db)
        {
            return db.AbacPolicies.Where(p => audit.EvaluatedPolicyIds.Contains(p.Id));
        }

        [Authorize(Policy = "Policies:Read")]
        [UseProjection]
        public IQueryable<AbacPolicy> GetMatchingPolicies(
            [Parent] AbacAudit audit,
            [Service] TechtonicCmsDbContext db)
        {
            return db.AbacPolicies.Where(p => audit.MatchingPolicyIds.Contains(p.Id));
        }
    }
}