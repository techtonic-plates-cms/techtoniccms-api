using BenchmarkDotNet.Attributes;
using Microsoft.EntityFrameworkCore;
using TechtonicCmsApi.Benchmarks.Infrastructure;
using TechtonicCmsApi.Contexts;
using TechtonicCmsApi.Schema.TechtonicCms.Enums;
using TechtonicCmsApi.Services;

namespace TechtonicCmsApi.Benchmarks.Benchmarks;

/// <summary>
/// Benchmark 3.3 — Overhead do Filtro Row-Level ABAC.
/// Mede o custo adicional da filtragem row-level sobre consultas que retornam coleções.
/// </summary>
[MemoryDiagnoser]
[RankColumn]
public class RowLevelFilterBenchmark : IDisposable
{
    private BenchmarkDbContextFactory _factory = null!;
    private Guid _userId;
    private Guid _otherUserId;
    private Guid _collectionId;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _factory = new BenchmarkDbContextFactory();
        _factory.ClearAllData();
        _factory.SeedBaseline(userCount: 2, collectionCount: 1, entryCount: 1_000);

        using var db = _factory.CreateDbContext();
        var users = db.Users.Take(2).ToList();
        _userId = users[0].Id;
        _otherUserId = users[1].Id;
        _collectionId = db.Collections.First().Id;

        // Update half the entries to belong to the other user
        var entriesToUpdate = db.Entries
            .Where(e => e.CollectionId == _collectionId)
            .OrderBy(e => e.Id)
            .Take(500)
            .ToList();

        foreach (var entry in entriesToUpdate)
            entry.CreatedBy = _otherUserId;

        db.SaveChanges();
    }

    [Benchmark(Baseline = true)]
    public async Task<int> BaselineQuery()
    {
        await using var db = _factory.CreateDbContext();
        return await db.Entries
            .Where(e => e.CollectionId == _collectionId)
            .CountAsync();
    }

    [Benchmark]
    public async Task<int> UnrestrictedQuery()
    {
        await using var db = _factory.CreateDbContext();
        var svc = new AbacService(db, new FakeHttpContextAccessor());
        var isRestricted = await svc.IsRestrictedToOwnResourcesAsync(_userId, BaseResource.Entries, PermissionAction.Read);

        var query = db.Entries.Where(e => e.CollectionId == _collectionId);
        if (isRestricted)
            query = query.Where(e => e.CreatedBy == _userId);

        return await query.CountAsync();
    }

    [Benchmark]
    public async Task<int> RestrictedQuery()
    {
        await using var db = _factory.CreateDbContext();
        // Simulate row-level restriction by injecting ownership filter
        return await db.Entries
            .Where(e => e.CollectionId == _collectionId && e.CreatedBy == _userId)
            .CountAsync();
    }

    [GlobalCleanup]
    public void Dispose()
    {
        _factory.ClearAllData();
    }
}
