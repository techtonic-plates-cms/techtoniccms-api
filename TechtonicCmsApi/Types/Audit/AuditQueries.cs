
using HotChocolate;
using HotChocolate.Authorization;
using TechtonicCmsApi.Contexts;
using TechtonicCmsApi.Schema.TechtonicCms;
using TechtonicCmsApi.Schema.TechtonicCms.Entities;
using TechtonicCmsApi.Schema.TechtonicCms.Enums;
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
    public async Task<AbacAudit?> Audit(
        Guid id,
        [Service] TechtonicCmsDbContext db,
        [Service] AbacService abacService,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userIdStr = httpContextAccessor.HttpContext?.User.FindFirst("userId")?.Value;
        if (userIdStr is null || !Guid.TryParse(userIdStr, out var userId))
            throw new GraphQLException(ErrorBuilder.New()
                .SetMessage("Authentication required")
                .SetCode("UNAUTHENTICATED")
                .Build());

        var audit = db.AbacAudits.Find(id);
        if (audit is null)
            return null;

        await abacService.RequirePermissionAsync(
            userId,
            BaseResource.Audits,
            PermissionAction.Read,
            new Dictionary<string, object?>
            {
                ["ResourceAuditUserId"] = audit.UserId?.ToString(),
            });

        return audit;
    }
}


[ExtendObjectType(nameof(Query))]

public static class AuditQueries
{
    public static AuditQuery Audit() => new();
}