
using HotChocolate.Authorization;
using TechtonicCmsApi.Contexts;
using TechtonicCmsApi.Schema.TechtonicCms.Entities;
using TechtonicCmsApi.Services;
using TechtonicCmsApi.Types;

public class AuditQuery
{
    [Authorize(Policy = "Audits:Read")]
    [UsePaging(MaxPageSize = 100)]
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public IQueryable<AbacAudit> Audits(
        [Service] TechtonicCmsDbContext db)
    {
        return db.AbacAudits;
    }

    [Authorize(Policy = "Audits:Read")]
    [UseProjection]
    public AbacAudit? Audit(
        Guid id,
        [Service] TechtonicCmsDbContext db,
        [Service] AbacService abacService,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var audit = db.AbacAudits.Find(id);

        return audit;
    }
}


[ExtendObjectType(nameof(Query))]

public static class AuditQueries
{
    public static AuditQuery Audit() => new();
}