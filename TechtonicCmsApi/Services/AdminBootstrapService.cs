using Microsoft.EntityFrameworkCore;
using TechtonicCmsApi.Contexts;
using TechtonicCmsApi.Schema.TechtonicCms.Entities;
using TechtonicCmsApi.Schema.TechtonicCms.Enums;

namespace TechtonicCmsApi.Services;

public static class AdminBootstrapService
{
    public static async Task SeedAsync(
        TechtonicCmsDbContext db,
        PasswordService passwordService,
        IConfiguration config)
    {
        var adminName = config["Admin:Name"] ?? "admin";
        var adminPassword = config["Admin:Password"];

        if (adminPassword is null or "")
        {
            throw new InvalidOperationException("Admin password must be provided in configuration under 'Admin:Password'");
        }

        var adminUser = await db.Users.FirstOrDefaultAsync(u => u.Name == adminName);

        if (adminUser is null)
        {
            var now = DateTime.UtcNow;
            adminUser = new User
            {
                Id = Guid.NewGuid(),
                Name = adminName,
                PasswordHash = passwordService.HashPassword(adminPassword),
                Status = UserStatus.Active,
                CreationTime = now,
                LastLoginTime = now,
                LastEditTime = now
            };

            db.Users.Add(adminUser);
            await db.SaveChangesAsync();

            Console.WriteLine($"Default admin user created: {adminName}");
            Console.WriteLine("Please change the password after first login!");
        }

        var adminRole = await db.Roles.FirstOrDefaultAsync(r => r.Name == "admin");

        if (adminRole is null)
        {
            var now = DateTime.UtcNow;
            adminRole = new Role
            {
                Id = Guid.NewGuid(),
                Name = "admin",
                Description = "Administrator role with full system access",
                CreationTime = now,
                LastEditTime = now
            };

            db.Roles.Add(adminRole);
            await db.SaveChangesAsync();

            Console.WriteLine("Admin role created");
        }

        // Seed wildcard *:* policy for admin role (super-admin access)
        // The admin role only needs this single wildcard policy instead of
        // individual policies for every resource/action combination.
        var wildcardPolicyName = "wildcard-all-resources-all-actions";
        var wildcardPolicyExists = await db.AbacPolicies.AnyAsync(p => p.Name == wildcardPolicyName);
        if (!wildcardPolicyExists)
        {
            var now = DateTime.UtcNow;
            var wildcardPolicy = new AbacPolicy
            {
                Id = Guid.NewGuid(),
                Name = wildcardPolicyName,
                Description = "Full access to all resources and actions (wildcard policy)",
                Effect = PermissionEffect.Allow,
                Priority = 9999,
                IsActive = true,
                ResourceType = BaseResource.Wildcard,
                ActionType = PermissionAction.Wildcard,
                RuleConnector = LogicalOperator.And,
                CreatedBy = adminUser.Id,
                CreatedAt = now,
                UpdatedAt = now
            };

            db.AbacPolicies.Add(wildcardPolicy);
            await db.SaveChangesAsync();

            db.RolePolicies.Add(new RolePolicy
            {
                Id = Guid.NewGuid(),
                RoleId = adminRole.Id,
                PolicyId = wildcardPolicy.Id,
                AssignedBy = adminUser.Id,
                AssignedAt = now,
                Reason = "Admin wildcard super-policy"
            });
            await db.SaveChangesAsync();
            Console.WriteLine("Admin wildcard policy created and assigned");
        }

        var creatorPolicies = new[]
        {
            (Name: "collections-read-by-creator",          Action: PermissionAction.Read),
            (Name: "collections-update-by-creator",        Action: PermissionAction.Update),
            (Name: "collections-delete-by-creator",        Action: PermissionAction.Delete),
            (Name: "collections-manageschema-by-creator",  Action: PermissionAction.ManageSchema),
        };

        foreach (var (name, action) in creatorPolicies)
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
                CreatedBy = adminUser.Id,
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
            (Name: "apikeys-read-by-owner",    Action: PermissionAction.Read),
            (Name: "apikeys-update-by-owner",  Action: PermissionAction.Update),
            (Name: "apikeys-delete-by-owner",  Action: PermissionAction.Delete),
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
                CreatedBy = adminUser.Id,
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

        // Seed entry creator policies
        var entryCreatorPolicies = new[]
        {
            (Name: "entries-read-by-creator",    Action: PermissionAction.Read),
            (Name: "entries-update-by-creator",  Action: PermissionAction.Update),
            (Name: "entries-delete-by-creator",  Action: PermissionAction.Delete),
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
                CreatedBy = adminUser.Id,
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

        var hasAdminRole = await db.UserRoles
            .AnyAsync(ur => ur.UserId == adminUser.Id && ur.RoleId == adminRole.Id);

        if (!hasAdminRole)
        {
            db.UserRoles.Add(new UserRole
            {
                Id = Guid.NewGuid(),
                UserId = adminUser.Id,
                RoleId = adminRole.Id,
                AssignedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
            Console.WriteLine("Admin role assigned to admin user");
        }
    }

}

