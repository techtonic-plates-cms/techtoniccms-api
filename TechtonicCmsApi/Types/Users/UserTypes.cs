using HotChocolate;
using HotChocolate.Types;

using Microsoft.EntityFrameworkCore;

using TechtonicCmsApi.Schema.TechtonicCms;
using TechtonicCmsApi.Schema.TechtonicCms.Entities;
using TechtonicCmsApi.Schema.TechtonicCms.Enums;

namespace TechtonicCmsApi.Types.Users;

public class RoleRefDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public DateTime? AssignedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

[ObjectType<RoleRefDto>]
public static partial class RoleRefType
{
    public static string GetId([Parent] RoleRefDto role) => role.Id.ToString();

    [GraphQLType<NonNullType<StringType>>]
    public static string GetName([Parent] RoleRefDto role) => role.Name;

    public static string? GetDescription([Parent] RoleRefDto role) => role.Description;

    public static string? GetAssignedAt([Parent] RoleRefDto role) =>
        role.AssignedAt?.ToUniversalTime().ToString("o");

    public static string? GetExpiresAt([Parent] RoleRefDto role) =>
        role.ExpiresAt?.ToUniversalTime().ToString("o");
}

[ObjectType<User>]
public static partial class UserType
{
    public static string GetId([Parent] User user) => user.Id.ToString();

    [GraphQLType<NonNullType<StringType>>]
    public static string GetName([Parent] User user) => user.Name;

    public static string GetStatus([Parent] User user) => user.Status.ToString().ToUpperInvariant();

    public static string? GetCreationTime([Parent] User user) =>
        user.CreationTime.ToUniversalTime().ToString("o");

    public static string? GetLastLoginTime([Parent] User user) =>
        user.LastLoginTime.ToUniversalTime().ToString("o");

    public static string? GetLastEditTime([Parent] User user) =>
        user.LastEditTime.ToUniversalTime().ToString("o");

    public static async Task<IEnumerable<RoleRefDto>> GetRoles(
        [Parent] User user,
        [Service] TechtonicCmsDbContext db)
    {
        var userRoles = await db.UserRoles
            .Include(ur => ur.Role)
            .Where(ur => ur.UserId == user.Id)
            .Select(ur => new RoleRefDto
            {
                Id = ur.Role.Id,
                Name = ur.Role.Name,
                Description = ur.Role.Description,
                AssignedAt = ur.AssignedAt,
                ExpiresAt = ur.ExpiresAt
            })
            .ToListAsync();

        return userRoles;
    }

}
