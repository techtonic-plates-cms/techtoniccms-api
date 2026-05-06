using BenchmarkDotNet.Attributes;
using TechtonicCmsApi.Benchmarks.Infrastructure;
using TechtonicCmsApi.Contexts;
using TechtonicCmsApi.Schema.TechtonicCms.Enums;
using TechtonicCmsApi.Services;

namespace TechtonicCmsApi.Benchmarks.Benchmarks;

/// <summary>
/// Benchmark 3.1 — Cache Hit vs. Cache Miss no Motor ABAC.
/// Demonstra a diferença de latência entre decisão cacheada e avaliação completa.
/// </summary>
[MemoryDiagnoser]
[RankColumn]
public class AbacCacheBenchmark : IDisposable
{
    private BenchmarkDbContextFactory _factory = null!;
    private Guid _userId;
    private Dictionary<string, object?> _resourceData = null!;

    private AbacService _svc = null!;
    private TechtonicCmsDbContext _db = null!;


    [GlobalSetup]
    public void GlobalSetup()
    {
        _factory = new BenchmarkDbContextFactory();
        _factory.ClearAllData();
        _factory.SeedBaseline(userCount: 1);

        using var db = _factory.CreateDbContext();
        _userId = db.Users.First().Id;

        // Seed one allow policy so full evaluation has something to evaluate
        _factory.SeedAllowPolicy(_userId, priority: 100);

        _resourceData = new Dictionary<string, object?>
        {
            ["ResourceEntryCreatedBy"] = _userId.ToString(),
            ["ResourceEntryStatus"] = EntryStatus.Published.ToString()
        };
    }


    [IterationSetup(Target = nameof(AbacCacheHit))]
    public void IterationSetupCacheHit()
    {
        _factory.ClearCache(_userId);
        _db = _factory.CreateDbContext();
        _svc = new AbacService(_db, new FakeHttpContextAccessor());

        // Popula o cache com uma decisão real, usando o mesmo caminho de código
        _svc.CheckPermissionAsync(
            _userId,
            BaseResource.Entries,
            PermissionAction.Read,
            _resourceData).GetAwaiter().GetResult();

        // Recria o serviço para garantir que não há estado em memória
        _db.Dispose();
        _db = _factory.CreateDbContext();
        _svc = new AbacService(_db, new FakeHttpContextAccessor());
    }

    [IterationSetup(Target = nameof(AbacCacheMiss))]
    public void IterationSetupCacheMiss()
    {
        _factory.ClearCache(_userId);
        _db = _factory.CreateDbContext();
        _svc = new AbacService(_db, new FakeHttpContextAccessor());
    }

    [IterationCleanup]
    public void IterationCleanup()
    {
        _db?.Dispose();
    }

    [Benchmark]
    public async Task<bool> AbacCacheHit()
    {
        return await _svc.CheckPermissionAsync(
            _userId,
            BaseResource.Entries,
            PermissionAction.Read,
            _resourceData);
    }

    [Benchmark(Baseline = true)]
    public async Task<bool> AbacCacheMiss()
    {
        return await _svc.CheckPermissionAsync(
            _userId,
            BaseResource.Entries,
            PermissionAction.Read,
            _resourceData);
    }
    [GlobalCleanup]
    public void Dispose()
    {
        _factory.ClearAllData();
    }
}
