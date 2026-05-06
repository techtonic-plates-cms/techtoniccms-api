using Microsoft.EntityFrameworkCore;
using TechtonicCmsApi.Contexts;
using TechtonicCmsApi.Schema.TechtonicCms.Entities;

namespace TechtonicCmsApi.Benchmarks.Infrastructure;

/// <summary>
/// DbContext variant that suppresses AbacAudit writes while keeping all other behavior.
/// Used in Benchmark 3.5 to isolate the cost of audit logging.
/// </summary>
public class NoAuditDbContext : TechtonicCmsDbContext
{
    public NoAuditDbContext(DbContextOptions<TechtonicCmsDbContext> options) : base(options) { }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var auditEntries = ChangeTracker
            .Entries<AbacAudit>()
            .ToList();

        foreach (var entry in auditEntries)
            entry.State = EntityState.Detached;

        return await base.SaveChangesAsync(cancellationToken);
    }
}
