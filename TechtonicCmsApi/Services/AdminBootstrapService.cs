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
                        Name = $"{resource.ToString().ToLowerInvariant()}-{action.ToString().ToLowerInvariant()}",
                        Description = $"Full access to {action.ToString().ToLowerInvariant()} on {resource.ToString().ToLowerInvariant()}",
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
                        ExpectedValue = nameof(AttributePath.SubjectId),
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

