using System.Text.Json;
using BenchmarkDotNet.Attributes;
using Microsoft.EntityFrameworkCore;
using TechtonicCmsApi.Benchmarks.Infrastructure;
using TechtonicCmsApi.Contexts;
using TechtonicCmsApi.Schema.TechtonicCms.Enums;

namespace TechtonicCmsApi.Benchmarks.Benchmarks;

/// <summary>
/// Benchmark 3.6 — Predicate Pushdown vs. Filtragem em Memória.
/// Demonstra o valor das funções cms_extract_* registradas no PostgreSQL.
/// </summary>
[MemoryDiagnoser]
[RankColumn]
public class JsonbFilterBenchmark : IDisposable
{
    private BenchmarkDbContextFactory _factory = null!;
    private Guid _collectionId;

    [Params(100, 1_000, 10_000)]
    public int EntryCount { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _factory = new BenchmarkDbContextFactory();
        _factory.ClearAllData();
        _factory.SeedBaseline(userCount: 1, collectionCount: 1, entryCount: EntryCount);

        using var db = _factory.CreateDbContext();
        _collectionId = db.Collections.First().Id;

        // Ensure ~10% of entries have title == "Hello"
        var helloEntries = db.Entries
            .Where(e => e.CollectionId == _collectionId)
            .OrderBy(e => e.Id)
            .Take(EntryCount / 10)
            .ToList();

        foreach (var entry in helloEntries)
        {
            var data = JsonSerializer.Deserialize<Dictionary<string, object>>(entry.Data);
            if (data is not null)
            {
                data["title"] = "Hello";
                entry.Data = JsonSerializer.SerializeToDocument(data);
            }
        }

        db.SaveChanges();
    }

    [Benchmark(Baseline = true)]
    public async Task<int> WithPredicatePushdown()
    {
        await using var db = _factory.CreateDbContext();
        return await db.Entries
            .Where(e => e.CollectionId == _collectionId)
            .Where(e => CmsDbFunctions.CmsExtractText(e.Data, "title") == "Hello")
            .CountAsync();
    }

    [Benchmark]
    public int WithInMemoryFilter()
    {
        using var db = _factory.CreateDbContext();
        var all = db.Entries
            .Where(e => e.CollectionId == _collectionId)
            .AsEnumerable();

        return all.Count(e =>
            e.Data.RootElement.TryGetProperty("title", out var t) &&
            t.GetString() == "Hello");
    }

    [GlobalCleanup]
    public void Dispose()
    {
        _factory.ClearAllData();
    }
}
