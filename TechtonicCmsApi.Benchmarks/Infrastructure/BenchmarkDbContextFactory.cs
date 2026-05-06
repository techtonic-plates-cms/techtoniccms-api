using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TechtonicCmsApi.Contexts;
using TechtonicCmsApi.Schema.TechtonicCms.Entities;
using TechtonicCmsApi.Schema.TechtonicCms.Enums;

namespace TechtonicCmsApi.Benchmarks.Infrastructure;

/// <summary>
/// Connects to the existing PostgreSQL instance, provides seeded
/// <see cref="TechtonicCmsDbContext"/> instances for benchmarks.
/// Uses <c>database</c> host resolved via /etc/hosts (Podman DNS).
/// </summary>
public sealed class BenchmarkDbContextFactory
{
    private static readonly string ConnectionString =
        "Host=database;Port=5432;Database=techtoniccms_benchmarks;Username=techtonic;Password=H34AGlTYqH";

    public TechtonicCmsDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<TechtonicCmsDbContext>()
            .UseNpgsql(ConnectionString, b => b.MigrationsAssembly("TechtonicCmsApi"))
            .Options;

        return new TechtonicCmsDbContext(options);
    }

    public TechtonicCmsDbContext CreateNoAuditDbContext()
    {
        var options = new DbContextOptionsBuilder<TechtonicCmsDbContext>()
            .UseNpgsql(ConnectionString, b => b.MigrationsAssembly("TechtonicCmsApi"))
            .Options;

        return new NoAuditDbContext(options);
    }

    public void SeedBaseline(
        int userCount = 1,
        int collectionCount = 1,
        int entryCount = 100)
    {
        using var db = CreateDbContext();
        var now = DateTime.UtcNow;

        // ── Users ───────────────────────────────────────────────
        var users = new List<User>();
        for (int i = 0; i < userCount; i++)
        {
            users.Add(new User
            {
                Id = Guid.NewGuid(),
                Name = $"benchmark-user-{i}-{Guid.NewGuid():N}",
                PasswordHash = "ignored",
                CreationTime = now,
                LastLoginTime = now,
                LastEditTime = now,
                Status = UserStatus.Active
            });
        }
        db.Users.AddRange(users);
        db.SaveChanges();

        // ── Collections ─────────────────────────────────────────
        var collections = new List<Collection>();
        for (int i = 0; i < collectionCount; i++)
        {
            collections.Add(new Collection
            {
                Id = Guid.NewGuid(),
                CreatedBy = users[0].Id,
                Name = $"benchmark-collection-{i}",
                Slug = $"bench-coll-{i}-{Guid.NewGuid():N}",
                DefaultLocale = Locale.En,
                SupportedLocales = [Locale.En.ToString()],
                IsLocalized = false,
                CreatedAt = now,
                UpdatedAt = now
            });
        }
        db.Collections.AddRange(collections);
        db.SaveChanges();

        // ── Entries ─────────────────────────────────────────────
        var entries = new List<Entry>();
        for (int i = 0; i < entryCount; i++)
        {
            var doc = JsonSerializer.SerializeToDocument(new
            {
                title = $"Entry {i}",
                body = $"Body content for entry number {i} used in benchmarks."
            });

            entries.Add(new Entry
            {
                Id = Guid.NewGuid(),
                CreatedAt = now,
                UpdatedAt = now,
                CreatedBy = users[0].Id,
                CollectionId = collections[0].Id,
                Name = $"Entry {i}",
                Slug = $"entry-{i}",
                Status = EntryStatus.Published,
                Locale = Locale.En,
                DefaultLocale = Locale.En,
                Data = doc
            });
        }
        db.Entries.AddRange(entries);
        db.SaveChanges();
    }

    public void SeedPolicies(
        Guid userId,
        int policyCount,
        PermissionEffect effect,
        int priority = 100)
    {
        using var db = CreateDbContext();
        var now = DateTime.UtcNow;

        var policies = new List<AbacPolicy>();
        var rules = new List<AbacPolicyRule>();

        for (int i = 0; i < policyCount; i++)
        {
            var policyId = Guid.NewGuid();
            policies.Add(new AbacPolicy
            {
                Id = policyId,
                Name = $"bench-policy-{effect}-{i}-{Guid.NewGuid():N}",
                Effect = effect,
                Priority = priority - i, // descending priority
                IsActive = true,
                ResourceType = BaseResource.Entries,
                ActionType = PermissionAction.Read,
                RuleConnector = LogicalOperator.And,
                CreatedBy = userId,
                CreatedAt = now,
                UpdatedAt = now
            });

            // One rule that always matches (SubjectId != empty)
            rules.Add(new AbacPolicyRule
            {
                Id = Guid.NewGuid(),
                PolicyId = policyId,
                AttributePath = AttributePath.SubjectId,
                Operator = OperatorType.Ne,
                ValueType = Schema.TechtonicCms.Enums.ValueType.String,
                ExpectedStringValue = Guid.Empty.ToString(),
                IsActive = true,
                Order = 0,
                CreatedAt = now
            });
        }

        db.AbacPolicies.AddRange(policies);
        db.AbacPolicyRules.AddRange(rules);
        db.SaveChanges();

        // Link policies directly to user
        var userPolicies = policies.Select(p => new UserPolicy
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PolicyId = p.Id,
            AssignedBy = userId,
            AssignedAt = now
        }).ToList();

        db.UserPolicies.AddRange(userPolicies);
        db.SaveChanges();
    }

    public void SeedDenyPolicy(
        Guid userId,
        int priority = 1000)
    {
        using var db = CreateDbContext();
        var now = DateTime.UtcNow;
        var policyId = Guid.NewGuid();

        var policy = new AbacPolicy
        {
            Id = policyId,
            Name = $"bench-deny-policy-{Guid.NewGuid():N}",
            Effect = PermissionEffect.Deny,
            Priority = priority,
            IsActive = true,
            ResourceType = BaseResource.Entries,
            ActionType = PermissionAction.Read,
            RuleConnector = LogicalOperator.And,
            CreatedBy = userId,
            CreatedAt = now,
            UpdatedAt = now
        };

        var rule = new AbacPolicyRule
        {
            Id = Guid.NewGuid(),
            PolicyId = policyId,
            AttributePath = AttributePath.SubjectId,
            Operator = OperatorType.Ne,
            ValueType = Schema.TechtonicCms.Enums.ValueType.String,
            ExpectedStringValue = Guid.Empty.ToString(),
            IsActive = true,
            Order = 0,
            CreatedAt = now
        };

        db.AbacPolicies.Add(policy);
        db.AbacPolicyRules.Add(rule);
        db.SaveChanges();

        db.UserPolicies.Add(new UserPolicy
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PolicyId = policyId,
            AssignedBy = userId,
            AssignedAt = now
        });
        db.SaveChanges();
    }

    public void SeedAllowPolicy(
        Guid userId,
        int priority = 100)
    {
        using var db = CreateDbContext();
        var now = DateTime.UtcNow;
        var policyId = Guid.NewGuid();

        var policy = new AbacPolicy
        {
            Id = policyId,
            Name = $"bench-allow-policy-{Guid.NewGuid():N}",
            Effect = PermissionEffect.Allow,
            Priority = priority,
            IsActive = true,
            ResourceType = BaseResource.Entries,
            ActionType = PermissionAction.Read,
            RuleConnector = LogicalOperator.And,
            CreatedBy = userId,
            CreatedAt = now,
            UpdatedAt = now
        };

        var rule = new AbacPolicyRule
        {
            Id = Guid.NewGuid(),
            PolicyId = policyId,
            AttributePath = AttributePath.SubjectId,
            Operator = OperatorType.Ne,
            ValueType = Schema.TechtonicCms.Enums.ValueType.String,
            ExpectedStringValue = Guid.Empty.ToString(),
            IsActive = true,
            Order = 0,
            CreatedAt = now
        };

        db.AbacPolicies.Add(policy);
        db.AbacPolicyRules.Add(rule);
        db.SaveChanges();

        db.UserPolicies.Add(new UserPolicy
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PolicyId = policyId,
            AssignedBy = userId,
            AssignedAt = now
        });
        db.SaveChanges();
    }

    public void SeedCacheHit(
        Guid userId,
        BaseResource resource,
        PermissionAction action,
        PermissionEffect decision)
    {
        using var db = CreateDbContext();
        var now = DateTime.UtcNow;
        var resourceId = new Guid("4709788a-6880-4a2d-9f52-0824f352495a"); // Entries fallback

        db.AbacEvaluationCaches.Add(new AbacEvaluationCache
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ResourceType = resource,
            ResourceId = resourceId,
            ActionType = action,
            Decision = decision,
            MatchingPolicyIds = [],
            EvaluationTimeMs = 0,
            ComputedAt = now,
            ExpiresAt = now.AddHours(1),
            ContextChecksum = "bench-checksum",
            PolicyVersions = ""
        });
        db.SaveChanges();
    }

    public void ClearCache(Guid userId)
    {
        using var db = CreateDbContext();
        db.AbacEvaluationCaches
            .Where(c => c.UserId == userId)
            .ExecuteDelete();
    }

    public void ClearPolicies(Guid userId)
    {
        using var db = CreateDbContext();
        var policyIds = db.UserPolicies
            .Where(up => up.UserId == userId)
            .Select(up => up.PolicyId)
            .ToList();

        db.UserPolicies.Where(up => up.UserId == userId).ExecuteDelete();
        db.AbacPolicyRules.Where(r => policyIds.Contains(r.PolicyId)).ExecuteDelete();
        db.AbacPolicies.Where(p => policyIds.Contains(p.Id)).ExecuteDelete();
    }

    public void ClearAllData()
    {
        using var db = CreateDbContext();
        db.EntryRelations.ExecuteDelete();
        db.Entries.ExecuteDelete();
        db.Fields.ExecuteDelete();
        db.Collections.ExecuteDelete();
        db.AbacEvaluationCaches.ExecuteDelete();
        db.AbacAudits.ExecuteDelete();
        db.AbacPolicyRules.ExecuteDelete();
        db.AbacPolicies.ExecuteDelete();
        db.UserPolicies.ExecuteDelete();
        db.RolePolicies.ExecuteDelete();
        db.UserRoles.ExecuteDelete();
        db.Roles.ExecuteDelete();
        db.Users.ExecuteDelete();
    }
}
