using Microsoft.EntityFrameworkCore;
using TechtonicCmsApi.Contexts;
using TechtonicCmsApi.Schema.TechtonicCms.Entities;
using TechtonicCmsApi.Schema.TechtonicCms.Enums;

namespace TechtonicCmsApi.Services;

public static class PolicyBootstrapService
{
    public static async Task SeedAsync(TechtonicCmsDbContext db, IConfiguration config)
    {
        var adminName = config["Admin:Name"] ?? "admin";
        var adminUser = await db.Users.FirstOrDefaultAsync(u => u.Name == adminName);
        var createdBy = adminUser?.Id ?? Guid.Empty;

        await SeedGenericPoliciesAsync(db, createdBy);
        await SeedCreatorPoliciesAsync(db, createdBy);
    }

    private static async Task SeedGenericPoliciesAsync(TechtonicCmsDbContext db, Guid createdBy)
    {
        var genericPolicies = new[]
        {
            (Name: "users-activate", Resource: BaseResource.Users, Action: PermissionAction.Activate),
            (Name: "users-deactivate", Resource: BaseResource.Users, Action: PermissionAction.Deactivate),
            (Name: "collections-create", Resource: BaseResource.Collections, Action: PermissionAction.Create),
            (Name: "collections-read", Resource: BaseResource.Collections, Action: PermissionAction.Read),
            (Name: "collections-update", Resource: BaseResource.Collections, Action: PermissionAction.Update),
            (Name: "collections-delete", Resource: BaseResource.Collections, Action: PermissionAction.Delete),
            (Name: "collections-manageschema", Resource: BaseResource.Collections, Action: PermissionAction.ManageSchema),
            (Name: "entries-create", Resource: BaseResource.Entries, Action: PermissionAction.Create),
            (Name: "entries-read", Resource: BaseResource.Entries, Action: PermissionAction.Read),
            (Name: "entries-update", Resource: BaseResource.Entries, Action: PermissionAction.Update),
            (Name: "entries-delete", Resource: BaseResource.Entries, Action: PermissionAction.Delete),
            (Name: "entries-publish", Resource: BaseResource.Entries, Action: PermissionAction.Publish),
            (Name: "entries-unpublish", Resource: BaseResource.Entries, Action: PermissionAction.Unpublish),
            (Name: "entries-schedule", Resource: BaseResource.Entries, Action: PermissionAction.Schedule),
            (Name: "entries-archive", Resource: BaseResource.Entries, Action: PermissionAction.Archive),
            (Name: "entries-restore", Resource: BaseResource.Entries, Action: PermissionAction.Restore),
            (Name: "assets-create", Resource: BaseResource.Assets, Action: PermissionAction.Create),
            (Name: "assets-read", Resource: BaseResource.Assets, Action: PermissionAction.Read),
            (Name: "assets-update", Resource: BaseResource.Assets, Action: PermissionAction.Update),
            (Name: "assets-delete", Resource: BaseResource.Assets, Action: PermissionAction.Delete),
            (Name: "assets-upload", Resource: BaseResource.Assets, Action: PermissionAction.Upload),
            (Name: "assets-download", Resource: BaseResource.Assets, Action: PermissionAction.Download),
            (Name: "apikeys-create", Resource: BaseResource.ApiKeys, Action: PermissionAction.Create),
        };

        foreach (var (name, resource, action) in genericPolicies)
        {
            var exists = await db.AbacPolicies.AnyAsync(p => p.Name == name);
            if (exists) continue;

            var now = DateTime.UtcNow;
            var policy = new AbacPolicy
            {
                Id = Guid.NewGuid(),
                Name = name,
                Description = $"Allow {action.ToString().ToLowerInvariant()} on {resource.ToString().ToLowerInvariant()}",
                Effect = PermissionEffect.Allow,
                Priority = 100,
                IsActive = true,
                ResourceType = resource,
                ActionType = action,
                RuleConnector = LogicalOperator.And,
                CreatedBy = createdBy,
                CreatedAt = now,
                UpdatedAt = now
            };

            db.AbacPolicies.Add(policy);
        }

        await db.SaveChangesAsync();
        Console.WriteLine("Generic default policies created");
    }

    private static async Task SeedCreatorPoliciesAsync(TechtonicCmsDbContext db, Guid createdBy)
    {
        var collectionCreatorPolicies = new[]
        {
            (Name: "collections-read-by-creator", Action: PermissionAction.Read),
            (Name: "collections-update-by-creator", Action: PermissionAction.Update),
            (Name: "collections-delete-by-creator", Action: PermissionAction.Delete),
            (Name: "collections-manageschema-by-creator", Action: PermissionAction.ManageSchema),
        };

        foreach (var (name, action) in collectionCreatorPolicies)
        {
            var exists = await db.AbacPolicies.AnyAsync(p => p.Name == name);
            if (exists) continue;

            var now = DateTime.UtcNow;
            var policyId = Guid.NewGuid();
            var policy = new AbacPolicy
            {
                Id = policyId,
                Name = name,
                Description = $"Allow collection {action.ToString().ToLowerInvariant()} for the collection's creator",
                Effect = PermissionEffect.Allow,
                Priority = 500,
                IsActive = true,
                ResourceType = BaseResource.Collections,
                ActionType = action,
                RuleConnector = LogicalOperator.And,
                CreatedBy = createdBy,
                CreatedAt = now,
                UpdatedAt = now,
                Rules =
                [
                    new AbacPolicyRule
                    {
                        Id = Guid.NewGuid(),
                        PolicyId = policyId,
                        AttributePath = AttributePath.ResourceCollectionCreatedBy,
                        Operator = OperatorType.EqContextRef,
                        ContextReferencePath = AttributePath.SubjectId,
                        ValueType = Schema.TechtonicCms.Enums.ValueType.Uuid,
                        IsActive = true,
                        Order = 1,
                        CreatedAt = now
                    }
                ]
            };

            db.AbacPolicies.Add(policy);
        }

        await db.SaveChangesAsync();

        var apiKeyCreatorPolicies = new[]
        {
            (Name: "apikeys-read-by-owner", Action: PermissionAction.Read),
            (Name: "apikeys-update-by-owner", Action: PermissionAction.Update),
            (Name: "apikeys-delete-by-owner", Action: PermissionAction.Delete),
            
        };

        foreach (var (name, action) in apiKeyCreatorPolicies)
        {
            var exists = await db.AbacPolicies.AnyAsync(p => p.Name == name);
            if (exists) continue;

            var now = DateTime.UtcNow;
            var policyId = Guid.NewGuid();
            var policy = new AbacPolicy
            {
                Id = policyId,
                Name = name,
                Description = $"Allow api key {action.ToString().ToLowerInvariant()} for the key's owner",
                Effect = PermissionEffect.Allow,
                Priority = 500,
                IsActive = true,
                ResourceType = BaseResource.ApiKeys,
                ActionType = action,
                RuleConnector = LogicalOperator.And,
                CreatedBy = createdBy,
                CreatedAt = now,
                UpdatedAt = now,
                Rules =
                [
                    new AbacPolicyRule
                    {
                        Id = Guid.NewGuid(),
                        PolicyId = policyId,
                        AttributePath = AttributePath.ResourceApiKeyUserId,
                        Operator = OperatorType.EqContextRef,
                        ContextReferencePath = AttributePath.SubjectId,
                        ValueType = Schema.TechtonicCms.Enums.ValueType.Uuid,
                        IsActive = true,
                        Order = 1,
                        CreatedAt = now
                    }
                ]
            };

            db.AbacPolicies.Add(policy);
        }

        await db.SaveChangesAsync();

        var entryCreatorPolicies = new[]
        {
            (Name: "entries-read-by-creator", Action: PermissionAction.Read),
            (Name: "entries-update-by-creator", Action: PermissionAction.Update),
            (Name: "entries-delete-by-creator", Action: PermissionAction.Delete),
            (Name: "entries-publish-by-creator", Action: PermissionAction.Publish),
            (Name: "entries-unpublish-by-creator", Action: PermissionAction.Unpublish),
        };

        foreach (var (name, action) in entryCreatorPolicies)
        {
            var exists = await db.AbacPolicies.AnyAsync(p => p.Name == name);
            if (exists) continue;

            var now = DateTime.UtcNow;
            var policyId = Guid.NewGuid();
            var policy = new AbacPolicy
            {
                Id = policyId,
                Name = name,
                Description = $"Allow entry {action.ToString().ToLowerInvariant()} for the entry's creator",
                Effect = PermissionEffect.Allow,
                Priority = 500,
                IsActive = true,
                ResourceType = BaseResource.Entries,
                ActionType = action,
                RuleConnector = LogicalOperator.And,
                CreatedBy = createdBy,
                CreatedAt = now,
                UpdatedAt = now,
                Rules =
                [
                    new AbacPolicyRule
                    {
                        Id = Guid.NewGuid(),
                        PolicyId = policyId,
                        AttributePath = AttributePath.ResourceEntryCreatedBy,
                        Operator = OperatorType.EqContextRef,
                        ContextReferencePath = AttributePath.SubjectId,
                        ValueType = Schema.TechtonicCms.Enums.ValueType.Uuid,
                        IsActive = true,
                        Order = 1,
                        CreatedAt = now
                    }
                ]
            };

            db.AbacPolicies.Add(policy);
        }

        await db.SaveChangesAsync();
        Console.WriteLine("Creator policies created");
    }
}
