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

