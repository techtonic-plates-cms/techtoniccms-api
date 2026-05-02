using HotChocolate;
using HotChocolate.Authorization;
using HotChocolate.Types;

using Microsoft.EntityFrameworkCore;
using TechtonicCmsApi.Contexts;
using TechtonicCmsApi.Schema.TechtonicCms;
using TechtonicCmsApi.Schema.TechtonicCms.Enums;
using TechtonicCmsApi.Services;

namespace TechtonicCmsApi.Types.ApiKeys;

public class CreateApiKeyInput
{
    [GraphQLType<NonNullType<StringType>>]
    public string Name { get; set; } = "";

    public Guid? UserId { get; set; }

    public DateTime? ExpiresAt { get; set; }
}

public class UpdateApiKeyInput
{
    [GraphQLType<NonNullType<IdType>>]
    public Guid Id { get; set; }

    public string? Name { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public bool? IsActive { get; set; }
}

public class ApiKeyMutation
{
    [Authorize("ApiKeys:Create")]
    public async Task<CreateApiKeyPayload> CreateApiKey(
        CreateApiKeyInput input,
        [Service] TechtonicCmsDbContext db,
        [Service] ApiKeyService apiKeyService,
        [Service] AbacService abacService,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var currentUserId = GetUserId(httpContextAccessor);
        var targetUserId = input.UserId ?? currentUserId;

        if (input.UserId.HasValue && input.UserId.Value != currentUserId)
        {
            var targetUser = await db.Users.FindAsync(targetUserId);
            if (targetUser is null)
                throw new GraphQLException(ErrorBuilder.New()
                    .SetMessage("User not found")
                    .SetCode("NOT_FOUND")
                    .Build());

            await abacService.RequirePermissionAsync(currentUserId, BaseResource.ApiKeys, PermissionAction.Create, new Dictionary<string, object?>
            {
                ["ResourceApiKeyUserId"] = targetUserId.ToString(),
            });
        }

        var (rawKey, hash, prefix) = apiKeyService.GenerateKey();
        var now = DateTime.UtcNow;

        var apiKey = new Schema.TechtonicCms.Entities.ApiKey
        {
            Id = Guid.NewGuid(),
            UserId = targetUserId,
            Name = input.Name,
            KeyHash = hash,
            KeyPrefix = prefix,
            CreatedAt = now,
            UpdatedAt = now,
            ExpiresAt = input.ExpiresAt is not null
                ?input.ExpiresAt
                : null,
            IsActive = true,
        };

        db.ApiKeys.Add(apiKey);
        await db.SaveChangesAsync();

        return new CreateApiKeyPayload
        {
            ApiKey = apiKey,
            Key = rawKey,
        };
    }

    [Authorize("ApiKeys:Update")]
    public async Task<Schema.TechtonicCms.Entities.ApiKey> UpdateApiKey(
        UpdateApiKeyInput input,
        [Service] TechtonicCmsDbContext db,
        [Service] AbacService abacService,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var currentUserId = GetUserId(httpContextAccessor);

        var apiKey = await db.ApiKeys.FindAsync(input.Id);
        if (apiKey is null)
            throw new GraphQLException(ErrorBuilder.New()
                .SetMessage("API key not found")
                .SetCode("NOT_FOUND")
                .Build());

        await abacService.RequirePermissionAsync(
            currentUserId,
            BaseResource.ApiKeys,
            PermissionAction.Update,
            new Dictionary<string, object?>
            {
                ["ResourceApiKeyUserId"] = apiKey.UserId.ToString(),
            });

        if (input.Name is not null)
            apiKey.Name = input.Name;

        if (input.ExpiresAt is not null)
            apiKey.ExpiresAt = input.ExpiresAt;

        if (input.IsActive.HasValue)
            apiKey.IsActive = input.IsActive.Value;

        apiKey.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return apiKey;
    }

    [Authorize("ApiKeys:Delete")]
    public async Task<bool> DeleteApiKey(
        Guid id,
        [Service] TechtonicCmsDbContext db,
        [Service] AbacService abacService,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var currentUserId = GetUserId(httpContextAccessor);

        var apiKey = await db.ApiKeys.FindAsync(id);
        if (apiKey is not null)
        {
            await abacService.RequirePermissionAsync(
                currentUserId,
                BaseResource.ApiKeys,
                PermissionAction.Delete,
                new Dictionary<string, object?>
                {
                    ["ResourceApiKeyUserId"] = apiKey.UserId.ToString(),
                });

            db.ApiKeys.Remove(apiKey);
            await db.SaveChangesAsync();
        }

        return true;
    }

    [Authorize("ApiKeys:Update")]
    public async Task<Schema.TechtonicCms.Entities.ApiKey> RevokeApiKey(
        Guid id,
        [Service] TechtonicCmsDbContext db,
        [Service] AbacService abacService,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var currentUserId = GetUserId(httpContextAccessor);

        var apiKey = await db.ApiKeys.FindAsync(id);
        if (apiKey is null)
            throw new GraphQLException(ErrorBuilder.New()
                .SetMessage("API key not found")
                .SetCode("NOT_FOUND")
                .Build());

        await abacService.RequirePermissionAsync(
            currentUserId,
            BaseResource.ApiKeys,
            PermissionAction.Update,
            new Dictionary<string, object?>
            {
                ["ResourceApiKeyUserId"] = apiKey.UserId.ToString(),
            });

        apiKey.IsActive = false;
        apiKey.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return apiKey;
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
public static class ApiKeyMutations
{
    public static ApiKeyMutation ApiKeys() => new();
}
