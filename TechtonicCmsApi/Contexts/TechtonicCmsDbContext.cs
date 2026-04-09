using Microsoft.EntityFrameworkCore;
using TechtonicCmsApi.Schema.TechtonicCms.Entities;
using TechtonicCmsApi.Schema.TechtonicCms.Enums;

namespace TechtonicCmsApi.Contexts;

public class TechtonicCmsDbContext : DbContext
{
    public TechtonicCmsDbContext(DbContextOptions<TechtonicCmsDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<AbacPolicy> AbacPolicies => Set<AbacPolicy>();
    public DbSet<AbacPolicyRule> AbacPolicyRules => Set<AbacPolicyRule>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePolicy> RolePolicies => Set<RolePolicy>();
    public DbSet<UserPolicy> UserPolicies => Set<UserPolicy>();
    public DbSet<ResourceOwnership> ResourceOwnerships => Set<ResourceOwnership>();
    public DbSet<AbacEvaluationCache> AbacEvaluationCaches => Set<AbacEvaluationCache>();
    public DbSet<AbacAudit> AbacAudits => Set<AbacAudit>();
    public DbSet<Collection> Collections => Set<Collection>();
    public DbSet<Field> Fields => Set<Field>();
    public DbSet<Entry> Entries => Set<Entry>();
    public DbSet<EntryRelation> EntryRelations => Set<EntryRelation>();

    public DbSet<Asset> Assets => Set<Asset>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        RegisterEnums(modelBuilder);
        ConfigureUser(modelBuilder);
        ConfigureRole(modelBuilder);
        ConfigureAbacPolicy(modelBuilder);
        ConfigureAbacPolicyRule(modelBuilder);
        ConfigureUserRole(modelBuilder);
        ConfigureRolePolicy(modelBuilder);
        ConfigureUserPolicy(modelBuilder);
        ConfigureResourceOwnership(modelBuilder);
        ConfigureAbacEvaluationCache(modelBuilder);
        ConfigureAbacAudit(modelBuilder);
        ConfigureCollection(modelBuilder);
        ConfigureField(modelBuilder);
        ConfigureEntry(modelBuilder);
        ConfigureEntryRelation(modelBuilder);
        ConfigureAsset(modelBuilder);
    }

    private static void RegisterEnums(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresEnum<UserStatus>();
        modelBuilder.HasPostgresEnum<PermissionAction>();
        modelBuilder.HasPostgresEnum<BaseResource>();
        modelBuilder.HasPostgresEnum<PermissionEffect>();
        modelBuilder.HasPostgresEnum<AttributePath>();
        modelBuilder.HasPostgresEnum<OperatorType>();
        modelBuilder.HasPostgresEnum<Schema.TechtonicCms.Enums.ValueType>();
        modelBuilder.HasPostgresEnum<LogicalOperator>();
        modelBuilder.HasPostgresEnum<EntryStatus>();
        modelBuilder.HasPostgresEnum<Locale>();
        modelBuilder.HasPostgresEnum<FieldDataType>();
    }

    private static void ConfigureUser(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.Property(u => u.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(u => u.CreationTime).HasDefaultValueSql("now()");
            e.Property(u => u.LastLoginTime).HasDefaultValueSql("now()");
            e.Property(u => u.LastEditTime).HasDefaultValueSql("now()");
            e.Property(u => u.Status).HasDefaultValue(UserStatus.Active);
        });
    }

    private static void ConfigureRole(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Role>(e =>
        {
            e.Property(r => r.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(r => r.CreationTime).HasDefaultValueSql("now()");
            e.Property(r => r.LastEditTime).HasDefaultValueSql("now()");
        });
    }

    private static void ConfigureAbacPolicy(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AbacPolicy>(e =>
        {
            e.Property(p => p.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(p => p.Priority).HasDefaultValue(100);
            e.Property(p => p.IsActive).HasDefaultValue(true);
            e.Property(p => p.RuleConnector).HasDefaultValue(LogicalOperator.And);
            e.Property(p => p.CreatedAt).HasDefaultValueSql("now()");
            e.Property(p => p.UpdatedAt).HasDefaultValueSql("now()");

            e.HasOne(p => p.CreatedByUser)
                .WithMany(u => u.CreatedPolicies)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureAbacPolicyRule(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AbacPolicyRule>(e =>
        {
            e.Property(r => r.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(r => r.IsActive).HasDefaultValue(true);
            e.Property(r => r.Order).HasDefaultValue(0);
            e.Property(r => r.CreatedAt).HasDefaultValueSql("now()");
        });
    }

    private static void ConfigureUserRole(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserRole>(e =>
        {
            e.Property(ur => ur.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(ur => ur.AssignedAt).HasDefaultValueSql("now()");
        });
    }

    private static void ConfigureRolePolicy(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RolePolicy>(e =>
        {
            e.Property(rp => rp.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(rp => rp.AssignedAt).HasDefaultValueSql("now()");

            e.HasOne(rp => rp.AssignedByUser)
                .WithMany(u => u.RolePoliciesAssignedBy)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureUserPolicy(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserPolicy>(e =>
        {
            e.Property(up => up.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(up => up.AssignedAt).HasDefaultValueSql("now()");

            e.HasOne(up => up.AssignedByUser)
                .WithMany(u => u.PoliciesAssignedBy)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureResourceOwnership(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ResourceOwnership>(e =>
        {
            e.Property(ro => ro.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(ro => ro.OwnershipType).HasDefaultValue("CREATOR");
            e.Property(ro => ro.AssignedAt).HasDefaultValueSql("now()");

            e.HasOne(ro => ro.AssignedByUser)
                .WithMany(u => u.ResourcesAssignedBy)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureAbacEvaluationCache(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AbacEvaluationCache>(e =>
        {
            e.Property(c => c.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(c => c.ComputedAt).HasDefaultValueSql("now()");
        });
    }

    private static void ConfigureAbacAudit(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AbacAudit>(e =>
        {
            e.Property(a => a.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(a => a.Timestamp).HasDefaultValueSql("now()");

            e.HasOne(a => a.User).WithMany(u => u.AuditLogs).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(a => a.Field).WithMany().OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureCollection(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Collection>(e =>
        {
            e.Property(c => c.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(c => c.DefaultLocale).HasDefaultValue(Locale.En);
            e.Property(c => c.SupportedLocales).HasDefaultValue(new[] { "en" });
            e.Property(c => c.IsLocalized).HasDefaultValue(false);
            e.Property(c => c.CreatedAt).HasDefaultValueSql("now()");
            e.Property(c => c.UpdatedAt).HasDefaultValueSql("now()");

            e.HasOne(c => c.CreatedByUser)
                .WithMany(u => u.CreatedCollections)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureField(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Field>(e =>
        {
            e.Property(f => f.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(f => f.IsRequired).HasDefaultValue(false);
            e.Property(f => f.IsUnique).HasDefaultValue(false);
            e.Property(f => f.IsPublic).HasDefaultValue(true);
            e.Property(f => f.IsPii).HasDefaultValue(false);
            e.Property(f => f.IsEncrypted).HasDefaultValue(false);
            e.Property(f => f.SensitivityLevel).HasDefaultValue("PUBLIC");
            e.Property(f => f.CreatedAt).HasDefaultValueSql("now()");
            e.Property(f => f.UpdatedAt).HasDefaultValueSql("now()");

            e.HasOne(f => f.CreatedByUser)
                .WithMany(u => u.CreatedFields)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(f => f.RelatedCollection)
                .WithMany()
                .HasForeignKey(f => f.RelatedCollectionId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureEntry(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Entry>(e =>
        {
            e.Property(en => en.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(en => en.CreatedAt).HasDefaultValueSql("now()");
            e.Property(en => en.UpdatedAt).HasDefaultValueSql("now()");
            e.Property(en => en.Status).HasDefaultValue(EntryStatus.Draft);
            e.Property(en => en.Locale).HasDefaultValue(Locale.En);
            e.Property(en => en.DefaultLocale).HasDefaultValue(Locale.En);

            e.HasOne(en => en.CreatedByUser)
                .WithMany(u => u.CreatedEntries)
                .OnDelete(DeleteBehavior.Restrict);
        });

        RegisterCmsDbFunctions(modelBuilder);
    }

    private static void RegisterCmsDbFunctions(ModelBuilder modelBuilder)
    {
        var functions = typeof(CmsDbFunctions);

        modelBuilder.HasDbFunction(functions.GetMethod(nameof(CmsDbFunctions.CmsExtractText))!)
            .HasName("cms_extract_text")
            .HasSchema("public");

        modelBuilder.HasDbFunction(functions.GetMethod(nameof(CmsDbFunctions.CmsExtractNumber))!)
            .HasName("cms_extract_number")
            .HasSchema("public");

        modelBuilder.HasDbFunction(functions.GetMethod(nameof(CmsDbFunctions.CmsExtractBoolean))!)
            .HasName("cms_extract_boolean")
            .HasSchema("public");

        modelBuilder.HasDbFunction(functions.GetMethod(nameof(CmsDbFunctions.CmsExtractDateTime))!)
            .HasName("cms_extract_datetime")
            .HasSchema("public");
    }

  


    private static void ConfigureEntryRelation(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EntryRelation>(e =>
        {
            e.Property(r => r.Id).HasDefaultValueSql("gen_random_uuid()");

            // Each field on an entry can have at most one relation target (1:1 field → target).
            e.HasIndex(r => new { r.EntryId, r.FieldId }).IsUnique();

            // Source entry → its relations. If the source entry is deleted, remove its relations.
            e.HasOne(r => r.Entry)
                .WithMany(en => en.FromRelations)
                .HasForeignKey(r => r.EntryId)
                .OnDelete(DeleteBehavior.Cascade);

            // Field definition → relations using it. Restrict: don't delete a field while relations exist.
            e.HasOne(r => r.Field)
                .WithMany(f => f.EntryRelations)
                .HasForeignKey(r => r.FieldId)
                .OnDelete(DeleteBehavior.Restrict);

            // Target entry → relations pointing to it. If the target is deleted, remove the relation.
            e.HasOne(r => r.TargetEntry)
                .WithMany(en => en.ToRelations)
                .HasForeignKey(r => r.TargetEntryId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }


    private static void ConfigureAsset(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Asset>(e =>
        {
            e.Property(a => a.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(a => a.UploadedAt).HasDefaultValueSql("now()");
            e.Property(a => a.IsPublic).HasDefaultValue(false);

            e.HasOne(a => a.UploadedByUser)
                .WithMany(u => u.UploadedAssets)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }


}
