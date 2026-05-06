using BenchmarkDotNet.Attributes;
using TechtonicCmsApi.Benchmarks.Infrastructure;
using TechtonicCmsApi.Schema.TechtonicCms.Enums;
using TechtonicCmsApi.Services;

namespace TechtonicCmsApi.Benchmarks.Benchmarks;

/// <summary>
/// Benchmark 3.2 — Escalabilidade do Motor ABAC por Número de Políticas.
/// Verifica se a complexidade O(p·q) se manifesta empiricamente e se o cache elimina essa dependência.
/// </summary>
[MemoryDiagnoser]
[RankColumn]
public class AbacPolicyScalingBenchmark : IDisposable
{
    private BenchmarkDbContextFactory _factory = null!;
    private Guid _userId;
    private Dictionary<string, object?> _resourceData = null!;

    [Params(1, 5, 10, 25, 50)]
    public int PolicyCount { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _factory = new BenchmarkDbContextFactory();
        _factory.ClearAllData();
        _factory.SeedBaseline(userCount: 1);

        using var db = _factory.CreateDbContext();
        _userId = db.Users.First().Id;

        // Seed policies once per parameter value
        _factory.SeedPolicies(_userId, PolicyCount, PermissionEffect.Allow, priority: 100);

        _resourceData = new Dictionary<string, object?>
        {
            ["ResourceEntryCreatedBy"] = _userId.ToString(),
            ["ResourceEntryStatus"] = EntryStatus.Published.ToString()
        };
    }

    [IterationSetup(Target = nameof(WithCache))]
    public void IterationSetupWithCache()
    {
        _factory.ClearCache(_userId);

        // Pre-populate cache so every invocation is a cache hit
        using var db = _factory.CreateDbContext();
        var svc = new AbacService(db, new FakeHttpContextAccessor());
        svc.CheckPermissionAsync(_userId, BaseResource.Entries, PermissionAction.Read, _resourceData).GetAwaiter().GetResult();
    }

    [IterationSetup(Target = nameof(WithoutCache))]
    public void IterationSetupWithoutCache()
    {
        _factory.ClearCache(_userId);
    }

    [Benchmark(Baseline = true)]
    public async Task<bool> WithCache()
    {
        await using var db = _factory.CreateDbContext();
        var svc = new AbacService(db, new FakeHttpContextAccessor());
        return await svc.CheckPermissionAsync(
            _userId, BaseResource.Entries, PermissionAction.Read, _resourceData);
    }

    [Benchmark]
    public async Task<bool> WithoutCache()
    {
        await using var db = _factory.CreateDbContext();
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
