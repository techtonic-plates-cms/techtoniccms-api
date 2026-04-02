using HotChocolate;
using HotChocolate.Authorization;
using HotChocolate.Types;

using Microsoft.EntityFrameworkCore;
using TechtonicCmsApi.Contexts;
using TechtonicCmsApi.Schema.TechtonicCms;
using TechtonicCmsApi.Schema.TechtonicCms.Entities;
using TechtonicCmsApi.Schema.TechtonicCms.Enums;
using TechtonicCmsApi.Services;

using UserEntity = TechtonicCmsApi.Schema.TechtonicCms.Entities.User;

namespace TechtonicCmsApi.Types.Users;

public class CreateUserInput
{
    [GraphQLType<NonNullType<StringType>>]
    public string Name { get; set; } = "";

    [GraphQLType<NonNullType<StringType>>]
    public string Password { get; set; } = "";

    public UserStatus? Status { get; set; }

    public Guid[]? RoleIds { get; set; }
}

public class UpdateUserInput
{
    [GraphQLType<NonNullType<IdType>>]
    public Guid Id { get; set; }

    public string? Name { get; set; }

    public UserStatus? Status { get; set; }
}

public class ChangePasswordInput
{
    public Guid? UserId { get; set; }

    public string? CurrentPassword { get; set; }

    [GraphQLType<NonNullType<StringType>>]
    public string NewPassword { get; set; } = "";
}

public class AssignRoleInput
{
    [GraphQLType<NonNullType<IdType>>]
    public Guid UserId { get; set; }

    [GraphQLType<NonNullType<IdType>>]
    public Guid RoleId { get; set; }

    public string? ExpiresAt { get; set; }
}

public class UserMutation
{
    [Authorize("Users:Create")]
    public async Task<UserEntity> Create(
        CreateUserInput input,
        [Service] TechtonicCmsDbContext db,
        [Service] AbacService abacService,
        [Service] PasswordService passwordService,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var currentUserId = GetUserId(httpContextAccessor);

        var passwordHash = passwordService.HashPassword(input.Password);

        var now = DateTime.UtcNow;
        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            Name = input.Name,
            PasswordHash = passwordHash,
            Status = input.Status ?? UserStatus.Active,
            CreationTime = now,
            LastLoginTime = now,
            LastEditTime = now
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        if (input.RoleIds is { Length: > 0 })
        {
            await abacService.RequirePermissionAsync(currentUserId, BaseResource.Users, PermissionAction.Update);

            foreach (var roleId in input.RoleIds)
            {
                db.UserRoles.Add(new UserRole
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    RoleId = roleId,
                    AssignedAt = DateTime.UtcNow
                });
            }

            await db.SaveChangesAsync();
        }

        return user;
    }

    [Authorize("Users:Update")]
    public async Task<UserEntity> Update(
        UpdateUserInput input,
        [Service] TechtonicCmsDbContext db,
        [Service] AbacService abacService,
        [Service] SessionService sessionService,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var currentUserId = GetUserId(httpContextAccessor);
        
        var user = await db.Users.FindAsync(input.Id);
        if (user is null)
            throw new GraphQLException(ErrorBuilder.New()
                .SetMessage("User not found")
                .SetCode("NOT_FOUND")
                .Build());

        if (input.Name is not null)
            user.Name = input.Name;

        if (input.Status is not null)
            user.Status = input.Status.Value;

        user.LastEditTime = DateTime.UtcNow;
        await db.SaveChangesAsync();

        if (input.Status is not null && input.Status != UserStatus.Active)
        {
            await sessionService.DeleteAllUserSessionsAsync(user.Id.ToString());
        }

        return user;
    }

    [Authorize("Users:Delete")]
    public async Task<bool> Delete(
        Guid id,
        [Service] TechtonicCmsDbContext db,
        [Service] AbacService abacService,
        [Service] SessionService sessionService,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var currentUserId = GetUserId(httpContextAccessor);

        if (currentUserId == id)
            throw new GraphQLException(ErrorBuilder.New()
                .SetMessage("Cannot delete your own user account")
                .SetCode("FORBIDDEN")
                .Build());

        await sessionService.DeleteAllUserSessionsAsync(id.ToString());

        var user = await db.Users.FindAsync(id);
        if (user is not null)
        {
            db.Users.Remove(user);
            await db.SaveChangesAsync();
        }

        return true;
    }

    public async Task<bool> ChangePassword(
        ChangePasswordInput input,
        [Service] TechtonicCmsDbContext db,
        [Service] AbacService abacService,
        [Service] PasswordService passwordService,
        [Service] SessionService sessionService,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var currentUserId = GetUserId(httpContextAccessor);
        var targetUserId = input.UserId ?? currentUserId;
        var isChangingOwnPassword = targetUserId == currentUserId;

        if (!isChangingOwnPassword)
            await abacService.RequirePermissionAsync(currentUserId, BaseResource.Users, PermissionAction.Update);

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == targetUserId);
        if (user is null)
            throw new GraphQLException(ErrorBuilder.New()
                .SetMessage("User not found")
                .SetCode("NOT_FOUND")
                .Build());

        if (isChangingOwnPassword)
        {
            if (string.IsNullOrEmpty(input.CurrentPassword))
                throw new GraphQLException(ErrorBuilder.New()
                    .SetMessage("Current password is required when changing your own password")
                    .SetCode("BAD_REQUEST")
                    .Build());

            var (isValid, _) = passwordService.VerifyPassword(input.CurrentPassword, user.PasswordHash);
            if (!isValid)
                throw new GraphQLException(ErrorBuilder.New()
                    .SetMessage("Current password is incorrect")
                    .SetCode("UNAUTHENTICATED")
                    .Build());
        }

        var newHash = passwordService.HashPassword(input.NewPassword);
        user.PasswordHash = newHash;
        user.LastEditTime = DateTime.UtcNow;
        await db.SaveChangesAsync();

        await sessionService.DeleteAllUserSessionsAsync(targetUserId.ToString());

        return true;
    }

    [Authorize("Users:Update")]
    public async Task<bool> AssignRole(
        AssignRoleInput input,
        [Service] TechtonicCmsDbContext db,
        [Service] AbacService abacService,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var currentUserId = GetUserId(httpContextAccessor);

        var user = await db.Users.FindAsync(input.UserId);
        if (user is null)
            throw new GraphQLException(ErrorBuilder.New()
                .SetMessage("User not found")
                .SetCode("NOT_FOUND")
                .Build());

        var role = await db.Roles.FindAsync(input.RoleId);
        if (role is null)
            throw new GraphQLException(ErrorBuilder.New()
                .SetMessage("Role not found")
                .SetCode("NOT_FOUND")
                .Build());

        var exists = await db.UserRoles
            .AnyAsync(ur => ur.UserId == input.UserId && ur.RoleId == input.RoleId);

        if (exists)
            throw new GraphQLException(ErrorBuilder.New()
                .SetMessage("Role already assigned to user")
                .SetCode("CONFLICT")
                .Build());

        db.UserRoles.Add(new UserRole
        {
            Id = Guid.NewGuid(),
            UserId = input.UserId,
            RoleId = input.RoleId,
            AssignedAt = DateTime.UtcNow,
            ExpiresAt = input.ExpiresAt is not null
                ? DateTime.Parse(input.ExpiresAt, null, System.Globalization.DateTimeStyles.RoundtripKind)
                : null
        });

        await db.SaveChangesAsync();

        return true;
    }

    [Authorize("Users:Update")]
    public async Task<bool> UnassignRole(
        Guid userId,
        Guid roleId,
        [Service] TechtonicCmsDbContext db,
        [Service] AbacService abacService,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var currentUserId = GetUserId(httpContextAccessor);

        var userRole = await db.UserRoles
            .FirstOrDefaultAsync(ur => ur.UserId == userId && ur.RoleId == roleId);

        if (userRole is not null)
        {
            db.UserRoles.Remove(userRole);
            await db.SaveChangesAsync();
        }

        return true;
    }

    [Authorize("Users:Activate")]
    public async Task<UserEntity> Activate(
        Guid id,
        [Service] TechtonicCmsDbContext db,
        [Service] AbacService abacService,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var currentUserId = GetUserId(httpContextAccessor);

        var user = await db.Users.FindAsync(id);
        if (user is null)
            throw new GraphQLException(ErrorBuilder.New()
                .SetMessage("User not found")
                .SetCode("NOT_FOUND")
                .Build());

        user.Status = UserStatus.Active;
        user.LastEditTime = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return user;
    }

    [Authorize("Users:Deactivate")]
    public async Task<UserEntity> Deactivate(
        Guid id,
        [Service] TechtonicCmsDbContext db,
        [Service] AbacService abacService,
        [Service] SessionService sessionService,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var currentUserId = GetUserId(httpContextAccessor);

        if (currentUserId == id)
            throw new GraphQLException(ErrorBuilder.New()
                .SetMessage("Cannot deactivate your own account")
                .SetCode("FORBIDDEN")
                .Build());

        var user = await db.Users.FindAsync(id);
        if (user is null)
            throw new GraphQLException(ErrorBuilder.New()
                .SetMessage("User not found")
                .SetCode("NOT_FOUND")
                .Build());

        user.Status = UserStatus.Inactive;
        user.LastEditTime = DateTime.UtcNow;
        await db.SaveChangesAsync();

        await sessionService.DeleteAllUserSessionsAsync(user.Id.ToString());

        return user;
    }
    [Authorize("Users:Ban")]
    public async Task<UserEntity> Ban(
        Guid id,
        [Service] TechtonicCmsDbContext db,
        [Service] AbacService abacService,
        [Service] SessionService sessionService,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var currentUserId = GetUserId(httpContextAccessor);

        if (currentUserId == id)
            throw new GraphQLException(ErrorBuilder.New()
                .SetMessage("Cannot ban your own account")
                .SetCode("FORBIDDEN")
                .Build());

        var user = await db.Users.FindAsync(id);
        if (user is null)
            throw new GraphQLException(ErrorBuilder.New()
                .SetMessage("User not found")
                .SetCode("NOT_FOUND")
                .Build());

        user.Status = UserStatus.Banned;
        user.LastEditTime = DateTime.UtcNow;
        await db.SaveChangesAsync();

        await sessionService.DeleteAllUserSessionsAsync(user.Id.ToString());

        return user;
    }

    [Authorize("Users:Unban")]
    public async Task<UserEntity> Unban(
        Guid id,
        [Service] TechtonicCmsDbContext db,
        [Service] AbacService abacService,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var currentUserId = GetUserId(httpContextAccessor);

        var user = await db.Users.FindAsync(id);
        if (user is null)
            throw new GraphQLException(ErrorBuilder.New()
                .SetMessage("User not found")
                .SetCode("NOT_FOUND")
                .Build());

        user.Status = UserStatus.Active;
        user.LastEditTime = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return user;
    }

    private static Guid GetUserId(IHttpContextAccessor httpContextAccessor)
    {
        var userIdStr = httpContextAccessor.HttpContext?.User.FindFirst("userId")?.Value;
        if (userIdStr is null || !Guid.TryParse(userIdStr, out var userId))
            throw new GraphQLException(ErrorBuilder.New()
                .SetMessage("Authentication required")
                .SetCode("UNAUTHENTICATED")
                .Build());

        return userId;
    }
}

[ExtendObjectType(nameof(Mutation))]
public static class UserMutations
{
    public static UserMutation Users() => new();
}
