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

        var policyNames = new[]
        {
            "collections-read",
            "entries-read",
            "assets-read",
            "assets-download",
        };

        foreach (var policyName in policyNames)
        {
            var policy = await db.AbacPolicies.FirstOrDefaultAsync(p => p.Name == policyName);
            if (policy is null)
            {
                Console.WriteLine($"Warning: Policy '{policyName}' not found, skipping assignment to viewer role");
                continue;
            }

            var alreadyAssigned = await db.RolePolicies.AnyAsync(rp => rp.RoleId == viewerRole.Id && rp.PolicyId == policy.Id);
            if (alreadyAssigned) continue;

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
        Console.WriteLine("Viewer policies assigned");
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

        var policyNames = new[]
        {
            "users-activate",
            "users-deactivate",
            "collections-read",
            "collections-update",
            "collections-delete",
            "collections-manageschema",
            "entries-create",
            "entries-read",
            "entries-update",
            "entries-delete",
            "entries-publish",
            "entries-unpublish",
            "entries-schedule",
            "entries-archive",
            "entries-restore",
            "assets-read",
            "assets-download",
            "assets-upload",
        };

        foreach (var policyName in policyNames)
        {
            var policy = await db.AbacPolicies.FirstOrDefaultAsync(p => p.Name == policyName);
            if (policy is null)
            {
                Console.WriteLine($"Warning: Policy '{policyName}' not found, skipping assignment to editor role");
                continue;
            }

            var alreadyAssigned = await db.RolePolicies.AnyAsync(rp => rp.RoleId == editorRole.Id && rp.PolicyId == policy.Id);
            if (alreadyAssigned) continue;

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
        Console.WriteLine("Editor policies assigned");
    }
}
