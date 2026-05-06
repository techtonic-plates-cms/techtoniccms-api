using BenchmarkDotNet.Attributes;
using TechtonicCmsApi.Benchmarks.Infrastructure;
using TechtonicCmsApi.Schema.TechtonicCms.Enums;
using TechtonicCmsApi.Services;

namespace TechtonicCmsApi.Benchmarks.Benchmarks;

/// <summary>
/// Benchmark 3.4 — Latência de Decisão Deny vs. Allow.
/// Verifica o short-circuit do algoritmo deny-overrides:
/// uma deny policy de alta prioridade deve ser mais rápida que
/// uma allow policy que precisa varrer todas as deny policies primeiro.
/// </summary>
[MemoryDiagnoser]
[RankColumn]
public class AbacDenyAllowBenchmark : IDisposable
{
    private BenchmarkDbContextFactory _factory = null!;
    private Guid _denyUserId;
    private Guid _allowUserId;
    private Dictionary<string, object?> _resourceData = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _factory = new BenchmarkDbContextFactory();
        _factory.ClearAllData();
        _factory.SeedBaseline(userCount: 2);

        using var db = _factory.CreateDbContext();
        var users = db.Users.Take(2).ToList();
        _denyUserId = users[0].Id;
        _allowUserId = users[1].Id;

        // User 0: high-priority deny + low-priority allows → short-circuit deny
        _factory.SeedDenyPolicy(_denyUserId, priority: 1000);
        _factory.SeedPolicies(_denyUserId, 10, PermissionEffect.Allow, priority: 100);

        // User 1: 10 denies + 1 low-priority allow → must scan all denies
        _factory.SeedPolicies(_allowUserId, 10, PermissionEffect.Deny, priority: 500);
        _factory.SeedAllowPolicy(_allowUserId, priority: 100);

        _resourceData = new Dictionary<string, object?>
        {
            ["ResourceEntryCreatedBy"] = _denyUserId.ToString(),
            ["ResourceEntryStatus"] = EntryStatus.Published.ToString()
        };
    }

    [IterationSetup]
    public void IterationSetup()
    {
        _factory.ClearCache(_denyUserId);
        _factory.ClearCache(_allowUserId);
    }

    [Benchmark(Baseline = true)]
    public async Task<bool> DenyFirst()
    {
        await using var db = _factory.CreateDbContext();
        var svc = new AbacService(db, new FakeHttpContextAccessor());
        return await svc.CheckPermissionAsync(
            _denyUserId, BaseResource.Entries, PermissionAction.Read, _resourceData);
    }

    [Benchmark]
    public async Task<bool> AllowAfterDenies()
    {
        await using var db = _factory.CreateDbContext();
        var svc = new AbacService(db, new FakeHttpContextAccessor());
        return await svc.CheckPermissionAsync(
            _allowUserId, BaseResource.Entries, PermissionAction.Read, _resourceData);
    }

    [GlobalCleanup]
    public void Dispose()
    {
        _factory.ClearAllData();
    }
}
