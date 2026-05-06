using BenchmarkDotNet.Attributes;
using TechtonicCmsApi.Benchmarks.Infrastructure;
using TechtonicCmsApi.Schema.TechtonicCms.Enums;
using TechtonicCmsApi.Services;

namespace TechtonicCmsApi.Benchmarks.Benchmarks;

/// <summary>
/// Benchmark 3.5 — Custo de Auditoria ABAC.
/// Mede o overhead da escrita em abac_audit sobre o tempo total de autorização.
/// </summary>
[MemoryDiagnoser]
[RankColumn]
public class AbacAuditBenchmark : IDisposable
{
    private BenchmarkDbContextFactory _factory = null!;
    private Guid _userId;
    private Dictionary<string, object?> _resourceData = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _factory = new BenchmarkDbContextFactory();
        _factory.ClearAllData();
        _factory.SeedBaseline(userCount: 1);

        using var db = _factory.CreateDbContext();
        _userId = db.Users.First().Id;

        // Seed 25 policies for a realistic evaluation workload
        _factory.SeedPolicies(_userId, 25, PermissionEffect.Allow, priority: 100);

        _resourceData = new Dictionary<string, object?>
        {
            ["ResourceEntryCreatedBy"] = _userId.ToString(),
            ["ResourceEntryStatus"] = EntryStatus.Published.ToString()
        };
    }

    [IterationSetup]
    public void IterationSetup()
    {
        _factory.ClearCache(_userId);
    }

    [Benchmark(Baseline = true)]
    public async Task<bool> WithAudit()
    {
        await using var db = _factory.CreateDbContext();
        var svc = new AbacService(db, new FakeHttpContextAccessor());
        return await svc.CheckPermissionAsync(
            _userId, BaseResource.Entries, PermissionAction.Read, _resourceData);
    }

    [Benchmark]
    public async Task<bool> WithoutAudit()
    {
        await using var db = _factory.CreateNoAuditDbContext();
        var svc = new AbacService(db, new FakeHttpContextAccessor());
        return await svc.CheckPermissionAsync(
            _userId, BaseResource.Entries, PermissionAction.Read, _resourceData);
    }

    [GlobalCleanup]
    public void Dispose()
    {
        _factory.ClearAllData();
    }
}
