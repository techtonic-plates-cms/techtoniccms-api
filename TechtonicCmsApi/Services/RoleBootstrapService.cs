using Microsoft.EntityFrameworkCore;
using TechtonicCmsApi.Contexts;
using TechtonicCmsApi.Schema.TechtonicCms.Entities;
using TechtonicCmsApi.Schema.TechtonicCms.Enums;

namespace TechtonicCmsApi.Services;

public static class RoleBootstrapService
{
    public static async Task SeedAsync(TechtonicCmsDbContext db)
    {
        var adminUser = await db.Users.FirstOrDefaultAsync(u => u.Name == "admin");
        var createdBy = adminUser?.Id ?? Guid.Empty;

        await SeedViewerRoleAsync(db, createdBy);
        await SeedEditorRoleAsync(db, createdBy);
    }

    private static async Task SeedViewerRoleAsync(TechtonicCmsDbContext db, Guid createdBy)
    {
        var viewerRole = await db.Roles.FirstOrDefaultAsync(r => r.Name == "viewer");
        if (viewerRole is not null)
        {
            return;
        }

        var now = DateTime.UtcNow;
        viewerRole = new Role
        {
            Id = Guid.NewGuid(),
            Name = "viewer",
            Description = "Read-only access to content",
            CreationTime = now,
            LastEditTime = now
        };

        db.Roles.Add(viewerRole);
        await db.SaveChangesAsync();

        Console.WriteLine("Viewer role created");

        var viewerPermissions = new[]
        {
            (Resource: BaseResource.Collections, Action: PermissionAction.Read),
            (Resource: BaseResource.Entries, Action: PermissionAction.Read),
            (Resource: BaseResource.Assets, Action: PermissionAction.Read),
            (Resource: BaseResource.Assets, Action: PermissionAction.Download),
        };

        foreach (var (resource, action) in viewerPermissions)
        {
            var policy = new AbacPolicy
            {
                Id = Guid.NewGuid(),
                Name = $"viewer-{resource.ToString().ToLowerInvariant()}-{action.ToString().ToLowerInvariant()}",
                Description = $"Allow {action.ToString().ToLowerInvariant()} on {resource.ToString().ToLowerInvariant()} for viewers",
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
            await db.SaveChangesAsync();

            db.RolePolicies.Add(new RolePolicy
            {
                Id = Guid.NewGuid(),
                RoleId = viewerRole.Id,
                PolicyId = policy.Id,
                AssignedBy = createdBy,
                AssignedAt = now,
                Reason = "Default viewer role permissions"
            });
        }

        await db.SaveChangesAsync();
        Console.WriteLine("Viewer policies created and assigned");
    }

    private static async Task SeedEditorRoleAsync(TechtonicCmsDbContext db, Guid createdBy)
    {
        var editorRole = await db.Roles.FirstOrDefaultAsync(r => r.Name == "editor");
        if (editorRole is not null)
        {
            return;
        }

        var now = DateTime.UtcNow;
        editorRole = new Role
        {
            Id = Guid.NewGuid(),
            Name = "editor",
            Description = "Content editing access with publishing capabilities",
            CreationTime = now,
            LastEditTime = now
        };

        db.Roles.Add(editorRole);
        await db.SaveChangesAsync();

        Console.WriteLine("Editor role created");

        var editorPermissions = new[]
        {
            (Resource: BaseResource.Collections, Action: PermissionAction.Read),
            (Resource: BaseResource.Collections, Action: PermissionAction.Update),
            (Resource: BaseResource.Entries, Action: PermissionAction.Create),
            (Resource: BaseResource.Entries, Action: PermissionAction.Read),
            (Resource: BaseResource.Entries, Action: PermissionAction.Update),
            (Resource: BaseResource.Entries, Action: PermissionAction.Publish),
            (Resource: BaseResource.Entries, Action: PermissionAction.Unpublish),
            (Resource: BaseResource.Entries, Action: PermissionAction.Schedule),
            (Resource: BaseResource.Entries, Action: PermissionAction.Archive),
            (Resource: BaseResource.Entries, Action: PermissionAction.Restore),
            (Resource: BaseResource.Assets, Action: PermissionAction.Read),
            (Resource: BaseResource.Assets, Action: PermissionAction.Download),
            (Resource: BaseResource.Assets, Action: PermissionAction.Upload),
        };

        foreach (var (resource, action) in editorPermissions)
        {
            var policy = new AbacPolicy
            {
                Id = Guid.NewGuid(),
                Name = $"editor-{resource.ToString().ToLowerInvariant()}-{action.ToString().ToLowerInvariant()}",
                Description = $"Allow {action.ToString().ToLowerInvariant()} on {resource.ToString().ToLowerInvariant()} for editors",
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
            await db.SaveChangesAsync();

            db.RolePolicies.Add(new RolePolicy
            {
                Id = Guid.NewGuid(),
                RoleId = editorRole.Id,
                PolicyId = policy.Id,
                AssignedBy = createdBy,
                AssignedAt = now,
                Reason = "Default editor role permissions"
            });
        }

        await db.SaveChangesAsync();
        Console.WriteLine("Editor policies created and assigned");
    }
}
