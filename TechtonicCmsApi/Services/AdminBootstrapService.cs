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
        var adminPassword = config["Admin:Password"] ?? "admin123";

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
            Console.WriteLine("Password: " + adminPassword);
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

            foreach (BaseResource resource in Enum.GetValues<BaseResource>())
            {
                foreach (PermissionAction action in Enum.GetValues<PermissionAction>())
                {
                    var policy = new AbacPolicy
                    {
                        Id = Guid.NewGuid(),
                        Name = $"admin-{resource.ToString().ToLowerInvariant()}-{action.ToString().ToLowerInvariant()}",
                        Description = $"Admin full access to {action.ToString().ToLowerInvariant()} on {resource.ToString().ToLowerInvariant()}",
                        Effect = PermissionEffect.Allow,
                        Priority = 1000,
                        IsActive = true,
                        ResourceType = resource,
                        ActionType = action,
                        RuleConnector = LogicalOperator.And,
                        CreatedBy = adminUser.Id,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    db.AbacPolicies.Add(policy);
                    await db.SaveChangesAsync();

                    db.RolePolicies.Add(new RolePolicy
                    {
                        Id = Guid.NewGuid(),
                        RoleId = adminRole.Id,
                        PolicyId = policy.Id,
                        AssignedBy = adminUser.Id,
                        AssignedAt = DateTime.UtcNow,
                        Reason = "Default admin role permissions"
                    });
                }
            }

            await db.SaveChangesAsync();
            Console.WriteLine("Admin policies created and assigned");
        }

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

        // ── Seed default field-level ABAC policies ───────────────
        await SeedDefaultFieldPoliciesAsync(db, adminUser);
    }

    /// <summary>
    /// Seeds sensible default field-level ABAC policies:
    /// <list type="bullet">
    ///   <item>Deny read/write of PII fields (priority 90)</item>
    ///   <item>Deny read/write of CONFIDENTIAL sensitivity fields (priority 80)</item>
    ///   <item>Deny read/write of RESTRICTED sensitivity fields (priority 75)</item>
    ///   <item>Allow read of PUBLIC fields (priority 50)</item>
    ///   <item>Allow read of INTERNAL sensitivity fields (priority 40)</item>
    /// </list>
    /// These are not assigned to any role by default. Assign them to roles
    /// to enforce field-level access control for non-admin users.
    /// </summary>
    private static async Task SeedDefaultFieldPoliciesAsync(
        TechtonicCmsDbContext db,
        User adminUser)
    {
        var fieldPolicyNames = new[]
        {
            "deny-pii-fields-read",
            "deny-pii-fields-update",
            "deny-confidential-fields-read",
            "deny-confidential-fields-update",
            "deny-restricted-fields-read",
            "deny-restricted-fields-update",
            "allow-public-fields-read",
            "allow-internal-fields-read"
        };

        var existingNames = await db.AbacPolicies
            .Where(p => fieldPolicyNames.Contains(p.Name))
            .Select(p => p.Name)
            .ToHashSetAsync();

        if (existingNames.Count == fieldPolicyNames.Length)
            return; // All field policies already seeded

        var now = DateTime.UtcNow;

        // ── Deny PII Read (priority 90) ──────────────────────────
        if (!existingNames.Contains("deny-pii-fields-read"))
        {
            var policy = new AbacPolicy
            {
                Id = Guid.NewGuid(),
                Name = "deny-pii-fields-read",
                Description = "Deny read access to fields marked as PII",
                Effect = PermissionEffect.Deny,
                Priority = 90,
                IsActive = true,
                ResourceType = BaseResource.Fields,
                ActionType = PermissionAction.Read,
                RuleConnector = LogicalOperator.And,
                CreatedBy = adminUser.Id,
                CreatedAt = now,
                UpdatedAt = now
            };
            db.AbacPolicies.Add(policy);
            await db.SaveChangesAsync();

            db.AbacPolicyRules.Add(new AbacPolicyRule
            {
                Id = Guid.NewGuid(),
                PolicyId = policy.Id,
                AttributePath = AttributePath.ResourceFieldIsPii,
                Operator = OperatorType.Eq,
                ExpectedValue = "true",
                ValueType = TechtonicCmsApi.Schema.TechtonicCms.Enums.ValueType.Boolean,
                Order = 1,
                IsActive = true,
                Description = "Field is marked as PII",
                CreatedAt = now
            });
        }

        // ── Deny PII Update (priority 90) ────────────────────────
        if (!existingNames.Contains("deny-pii-fields-update"))
        {
            var policy = new AbacPolicy
            {
                Id = Guid.NewGuid(),
                Name = "deny-pii-fields-update",
                Description = "Deny write access to fields marked as PII",
                Effect = PermissionEffect.Deny,
                Priority = 90,
                IsActive = true,
                ResourceType = BaseResource.Fields,
                ActionType = PermissionAction.Update,
                RuleConnector = LogicalOperator.And,
                CreatedBy = adminUser.Id,
                CreatedAt = now,
                UpdatedAt = now
            };
            db.AbacPolicies.Add(policy);
            await db.SaveChangesAsync();

            db.AbacPolicyRules.Add(new AbacPolicyRule
            {
                Id = Guid.NewGuid(),
                PolicyId = policy.Id,
                AttributePath = AttributePath.ResourceFieldIsPii,
                Operator = OperatorType.Eq,
                ExpectedValue = "true",
                ValueType = TechtonicCmsApi.Schema.TechtonicCms.Enums.ValueType.Boolean,
                Order = 1,
                IsActive = true,
                Description = "Field is marked as PII",
                CreatedAt = now
            });
        }

        // ── Deny CONFIDENTIAL Read (priority 80) ─────────────────
        if (!existingNames.Contains("deny-confidential-fields-read"))
        {
            var policy = new AbacPolicy
            {
                Id = Guid.NewGuid(),
                Name = "deny-confidential-fields-read",
                Description = "Deny read access to fields with CONFIDENTIAL sensitivity level",
                Effect = PermissionEffect.Deny,
                Priority = 80,
                IsActive = true,
                ResourceType = BaseResource.Fields,
                ActionType = PermissionAction.Read,
                RuleConnector = LogicalOperator.And,
                CreatedBy = adminUser.Id,
                CreatedAt = now,
                UpdatedAt = now
            };
            db.AbacPolicies.Add(policy);
            await db.SaveChangesAsync();

            db.AbacPolicyRules.Add(new AbacPolicyRule
            {
                Id = Guid.NewGuid(),
                PolicyId = policy.Id,
                AttributePath = AttributePath.ResourceFieldSensitivityLevel,
                Operator = OperatorType.Eq,
                ExpectedValue = "CONFIDENTIAL",
                ValueType = TechtonicCmsApi.Schema.TechtonicCms.Enums.ValueType.String,
                Order = 1,
                IsActive = true,
                Description = "Field sensitivity is CONFIDENTIAL",
                CreatedAt = now
            });
        }

        // ── Deny CONFIDENTIAL Update (priority 80) ───────────────
        if (!existingNames.Contains("deny-confidential-fields-update"))
        {
            var policy = new AbacPolicy
            {
                Id = Guid.NewGuid(),
                Name = "deny-confidential-fields-update",
                Description = "Deny write access to fields with CONFIDENTIAL sensitivity level",
                Effect = PermissionEffect.Deny,
                Priority = 80,
                IsActive = true,
                ResourceType = BaseResource.Fields,
                ActionType = PermissionAction.Update,
                RuleConnector = LogicalOperator.And,
                CreatedBy = adminUser.Id,
                CreatedAt = now,
                UpdatedAt = now
            };
            db.AbacPolicies.Add(policy);
            await db.SaveChangesAsync();

            db.AbacPolicyRules.Add(new AbacPolicyRule
            {
                Id = Guid.NewGuid(),
                PolicyId = policy.Id,
                AttributePath = AttributePath.ResourceFieldSensitivityLevel,
                Operator = OperatorType.Eq,
                ExpectedValue = "CONFIDENTIAL",
                ValueType = TechtonicCmsApi.Schema.TechtonicCms.Enums.ValueType.String,
                Order = 1,
                IsActive = true,
                Description = "Field sensitivity is CONFIDENTIAL",
                CreatedAt = now
            });
        }

        // ── Deny RESTRICTED Read (priority 75) ────────────────────
        if (!existingNames.Contains("deny-restricted-fields-read"))
        {
            var policy = new AbacPolicy
            {
                Id = Guid.NewGuid(),
                Name = "deny-restricted-fields-read",
                Description = "Deny read access to fields with RESTRICTED sensitivity level",
                Effect = PermissionEffect.Deny,
                Priority = 75,
                IsActive = true,
                ResourceType = BaseResource.Fields,
                ActionType = PermissionAction.Read,
                RuleConnector = LogicalOperator.And,
                CreatedBy = adminUser.Id,
                CreatedAt = now,
                UpdatedAt = now
            };
            db.AbacPolicies.Add(policy);
            await db.SaveChangesAsync();

            db.AbacPolicyRules.Add(new AbacPolicyRule
            {
                Id = Guid.NewGuid(),
                PolicyId = policy.Id,
                AttributePath = AttributePath.ResourceFieldSensitivityLevel,
                Operator = OperatorType.Eq,
                ExpectedValue = "RESTRICTED",
                ValueType = TechtonicCmsApi.Schema.TechtonicCms.Enums.ValueType.String,
                Order = 1,
                IsActive = true,
                Description = "Field sensitivity is RESTRICTED",
                CreatedAt = now
            });
        }

        // ── Deny RESTRICTED Update (priority 75) ──────────────────
        if (!existingNames.Contains("deny-restricted-fields-update"))
        {
            var policy = new AbacPolicy
            {
                Id = Guid.NewGuid(),
                Name = "deny-restricted-fields-update",
                Description = "Deny write access to fields with RESTRICTED sensitivity level",
                Effect = PermissionEffect.Deny,
                Priority = 75,
                IsActive = true,
                ResourceType = BaseResource.Fields,
                ActionType = PermissionAction.Update,
                RuleConnector = LogicalOperator.And,
                CreatedBy = adminUser.Id,
                CreatedAt = now,
                UpdatedAt = now
            };
            db.AbacPolicies.Add(policy);
            await db.SaveChangesAsync();

            db.AbacPolicyRules.Add(new AbacPolicyRule
            {
                Id = Guid.NewGuid(),
                PolicyId = policy.Id,
                AttributePath = AttributePath.ResourceFieldSensitivityLevel,
                Operator = OperatorType.Eq,
                ExpectedValue = "RESTRICTED",
                ValueType = TechtonicCmsApi.Schema.TechtonicCms.Enums.ValueType.String,
                Order = 1,
                IsActive = true,
                Description = "Field sensitivity is RESTRICTED",
                CreatedAt = now
            });
        }

        // ── Allow Public Fields Read (priority 50) ────────────────
        if (!existingNames.Contains("allow-public-fields-read"))
        {
            var policy = new AbacPolicy
            {
                Id = Guid.NewGuid(),
                Name = "allow-public-fields-read",
                Description = "Allow read access to fields marked as public",
                Effect = PermissionEffect.Allow,
                Priority = 50,
                IsActive = true,
                ResourceType = BaseResource.Fields,
                ActionType = PermissionAction.Read,
                RuleConnector = LogicalOperator.And,
                CreatedBy = adminUser.Id,
                CreatedAt = now,
                UpdatedAt = now
            };
            db.AbacPolicies.Add(policy);
            await db.SaveChangesAsync();

            db.AbacPolicyRules.Add(new AbacPolicyRule
            {
                Id = Guid.NewGuid(),
                PolicyId = policy.Id,
                AttributePath = AttributePath.ResourceFieldIsPublic,
                Operator = OperatorType.Eq,
                ExpectedValue = "true",
                ValueType = TechtonicCmsApi.Schema.TechtonicCms.Enums.ValueType.Boolean,
                Order = 1,
                IsActive = true,
                Description = "Field is marked as public",
                CreatedAt = now
            });
        }

        // ── Allow Internal Fields Read (priority 40) ──────────────
        if (!existingNames.Contains("allow-internal-fields-read"))
        {
            var policy = new AbacPolicy
            {
                Id = Guid.NewGuid(),
                Name = "allow-internal-fields-read",
                Description = "Allow read access to fields with INTERNAL sensitivity level",
                Effect = PermissionEffect.Allow,
                Priority = 40,
                IsActive = true,
                ResourceType = BaseResource.Fields,
                ActionType = PermissionAction.Read,
                RuleConnector = LogicalOperator.And,
                CreatedBy = adminUser.Id,
                CreatedAt = now,
                UpdatedAt = now
            };
            db.AbacPolicies.Add(policy);
            await db.SaveChangesAsync();

            db.AbacPolicyRules.Add(new AbacPolicyRule
            {
                Id = Guid.NewGuid(),
                PolicyId = policy.Id,
                AttributePath = AttributePath.ResourceFieldSensitivityLevel,
                Operator = OperatorType.Eq,
                ExpectedValue = "INTERNAL",
                ValueType = TechtonicCmsApi.Schema.TechtonicCms.Enums.ValueType.String,
                Order = 1,
                IsActive = true,
                Description = "Field sensitivity is INTERNAL",
                CreatedAt = now
            });
        }

        await db.SaveChangesAsync();
        Console.WriteLine("Default field-level ABAC policies seeded");
    }
}
