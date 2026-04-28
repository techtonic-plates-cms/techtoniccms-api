# Plan: ABAC Middleware Attributes + MyKeys Query

## Overview

1. **`[AbacRequirePermission]`** — Pre-resolver middleware attribute that calls `AbacService.RequirePermissionAsync` before the resolver executes. Replaces the manual `await abacService.RequirePermissionAsync(...)` boilerplate in mutations and single-item queries.

2. **`[UseAbacRowCheck]`** — Post-resolver middleware attribute that validates every node in a paged result set after filtering/sorting/paging execute. If any returned row would be denied by ABAC, the entire query throws `FORBIDDEN`.

3. **`myKeys` query** — Convenience query on `AuthQuery` that returns only the current user's API keys.

## Files to Create/Modify

### 1. `TechtonicCmsApi/Security/AbacRequirePermissionAttribute.cs` (NEW)

Pre-resolver middleware attribute. Runs before the resolver, extracts the user ID from the HTTP context, and calls `AbacService.RequirePermissionAsync`.

```csharp
[AttributeUsage(AttributeTargets.Method)]
public class AbacRequirePermissionAttribute : ObjectFieldDescriptorAttribute
{
    public BaseResource Resource { get; }
    public PermissionAction Action { get; }

    public AbacRequirePermissionAttribute(BaseResource resource, PermissionAction action)
    {
        Resource = resource;
        Action = action;
    }

    protected override void OnConfigure(IDescriptorContext context, IObjectFieldDescriptor descriptor, MemberInfo member)
    {
        descriptor.Use(next => async ctx =>
        {
            var abacService = ctx.Services.GetRequiredService<AbacService>();
            var httpContextAccessor = ctx.Services.GetRequiredService<IHttpContextAccessor>();

            var userIdStr = httpContextAccessor.HttpContext?.User.FindFirst("userId")?.Value;
            if (userIdStr is null || !Guid.TryParse(userIdStr, out var userId))
                throw new GraphQLException(ErrorBuilder.New()
                    .SetMessage("Authentication required")
                    .SetCode("UNAUTHENTICATED")
                    .Build());

            await abacService.RequirePermissionAsync(userId, Resource, Action);

            await next(ctx);
        });
    }
}
```

**Usage example** (replacing manual call in `CollectionMutations.Create`):
```csharp
// Before:
[Authorize(Policy = "Collections:Create")]
public async Task<Collection> Create(...)
{
    var userId = GetUserId(httpContextAccessor);
    await abacService.RequirePermissionAsync(userId, BaseResource.Collections, PermissionAction.Create);
    // ...
}

// After:
[Authorize(Policy = "Collections:Create")]
[AbacRequirePermission(BaseResource.Collections, PermissionAction.Create)]
public async Task<Collection> Create(...)
{
    // No manual ABAC call needed
    // ...
}
```

**Note:** For mutations that need resource-specific context (e.g., `Update`, `Delete` where the entity must be fetched first), keep the manual `RequirePermissionAsync` call inside the resolver body. The attribute is for blanket checks only.

**Application sites** (replace redundant manual blanket checks):
- `CollectionMutations.Create` — remove manual `RequirePermissionAsync`
- `ApiKeyMutations.Create` — remove manual `RequirePermissionAsync` if present
- `RoleMutations.Create` — remove manual `RequirePermissionAsync` if present
- `PolicyMutations.Create` — remove manual `RequirePermissionAsync` if present
- Any other mutation where the manual call has no `resourceData` parameter

### 2. `TechtonicCmsApi/Security/AbacRowCheckMiddleware.cs` (NEW)

Create a custom field middleware attribute:

```csharp
[AttributeUsage(AttributeTargets.Method)]
public class UseAbacRowCheckAttribute : ObjectFieldDescriptorAttribute
{
    public BaseResource Resource { get; }
    public PermissionAction Action { get; }

    public UseAbacRowCheckAttribute(BaseResource resource, PermissionAction action)
    {
        Resource = resource;
        Action = action;
    }

    protected override void OnConfigure(IDescriptorContext context, IObjectFieldDescriptor descriptor, MemberInfo member)
    {
        descriptor.Use(next => async ctx =>
        {
            await next(ctx);
            // After full pipeline (filtering, sorting, paging) has executed,
            // ctx.Result contains Connection<T> (materialized)
            if (ctx.Result is null) return;

            var abacService = ctx.Services.GetRequiredService<AbacService>();
            var httpContextAccessor = ctx.Services.GetRequiredService<IHttpContextAccessor>();
            var userIdStr = httpContextAccessor.HttpContext?.User.FindFirst("userId")?.Value;
            if (userIdStr is null || !Guid.TryParse(userIdStr, out var userId))
                throw new GraphQLException(ErrorBuilder.New().SetMessage("Authentication required").SetCode("UNAUTHENTICATED").Build());

            // Use reflection to extract nodes from Connection<T>
            var edgesProp = ctx.Result.GetType().GetProperty("Edges");
            if (edgesProp is null) return;
            var edges = edgesProp.GetValue(ctx.Result) as IEnumerable;
            if (edges is null) return;

            foreach (var edge in edges)
            {
                var node = edge.GetType().GetProperty("Node")?.GetValue(edge);
                if (node is null) continue;

                var resourceData = abacService.ExtractResourceData(node, Resource);
                var allowed = await abacService.CheckPermissionAsync(userId, Resource, Action, resourceData);
                if (!allowed)
                {
                    throw new GraphQLException(ErrorBuilder.New()
                        .SetMessage($"Permission denied: result set contains {Resource} records that violate ABAC policies")
                        .SetCode("FORBIDDEN")
                        .Build());
                }
            }
        });
    }
}
```

**Attribute ordering:** Place `[UseAbacRowCheck]` as the **outermost** attribute:
```csharp
[UseAbacRowCheck(BaseResource.ApiKeys, PermissionAction.Read)]
[UsePaging(MaxPageSize = 100)]
[UseFiltering]
[UseSorting]
```

### 3. `TechtonicCmsApi/Services/AbacService.cs` — Add `ExtractResourceData` (MODIFY)

Add a new public method that maps entity instances to ABAC context dictionaries:

```csharp
public Dictionary<string, object?> ExtractResourceData(object entity, BaseResource resource)
{
    return (entity, resource) switch
    {
        (Schema.TechtonicCms.Entities.ApiKey apiKey, BaseResource.ApiKeys) => new Dictionary<string, object?>
        {
            ["ResourceApiKeyUserId"] = apiKey.UserId.ToString(),
            ["ResourceApiKeyId"] = apiKey.Id.ToString(),
            ["ResourceApiKeyMimeType"] = null,
            ["ResourceAssetFileSize"] = null,
        },
        (Schema.TechtonicCms.Entities.User user, BaseResource.Users) => new Dictionary<string, object?>
        {
            ["ResourceUserId"] = user.Id.ToString(),
            ["ResourceUserStatus"] = user.Status.ToString(),
        },
        (Schema.TechtonicCms.Entities.Asset asset, BaseResource.Assets) => new Dictionary<string, object?>
        {
            ["ResourceAssetUploadedBy"] = asset.UploadedBy.ToString(),
            ["ResourceAssetMimeType"] = asset.MimeType,
            ["ResourceAssetFileSize"] = asset.FileSize,
        },
        // Future: Entries, Collections, etc.
        _ => new Dictionary<string, object?>()
    };
}
```

### 4. `TechtonicCmsApi/Types/Auth/AuthQueries.cs` — Add `MyKeys` (MODIFY)

Add a new query method inside `AuthQuery`:

```csharp
[Authorize]
[UsePaging(MaxPageSize = 100)]
[UseFiltering]
[UseSorting]
public IQueryable<Schema.TechtonicCms.Entities.ApiKey> MyKeys(
    [Service] TechtonicCmsDbContext db,
    [Service] IHttpContextAccessor httpContextAccessor)
{
    var userIdStr = httpContextAccessor.HttpContext?.User.FindFirst("userId")?.Value;
    if (userIdStr is null || !Guid.TryParse(userIdStr, out var userId))
        throw new GraphQLException(ErrorBuilder.New()
            .SetMessage("Authentication required")
            .SetCode("UNAUTHENTICATED")
            .Build());

    return db.ApiKeys.Where(a => a.UserId == userId);
}
```

### 5. `TechtonicCmsApi/Types/ApiKeys/ApiKeyQueries.cs` — Apply Middleware (MODIFY)

Update the `ApiKeys()` list query:

- Add `[UseAbacRowCheck(BaseResource.ApiKeys, PermissionAction.Read)]` as the outermost attribute
- Remove the `abacService.RequirePermissionAsync` call from the method body (replaced by `[AbacRequirePermission]` or `[Authorize]`)
- Keep `[Authorize("ApiKeys:Read")]` for the blanket permission check, OR replace both with `[AbacRequirePermission(BaseResource.ApiKeys, PermissionAction.Read)]`

**Recommended approach:** Use `[Authorize]` for authN (authentication gate) and `[AbacRequirePermission]` for authZ (authorization gate):
```csharp
[Authorize]  // ensures user is authenticated
[AbacRequirePermission(BaseResource.ApiKeys, PermissionAction.Read)]  // blanket ABAC check
[UseAbacRowCheck(BaseResource.ApiKeys, PermissionAction.Read)]  // row-level validation
[UsePaging(MaxPageSize = 100)]
[UseFiltering]
[UseSorting]
public IQueryable<ApiKey> ApiKeys(...)
```

The `ApiKey(id)` single-item query keeps its inline per-item ABAC check (it needs resource context).

### 6. `TechtonicCmsApi/Program.cs` — Register Middleware (MODIFY)

Ensure the middleware attribute is discoverable. Hot Chocolate should pick it up automatically via the attribute, but verify `AddTypes()` scans the assembly.

No explicit registration needed if using `ObjectFieldDescriptorAttribute` pattern.

## How It Works

### Pre-Resolver (`[AbacRequirePermission]`)

1. User calls a mutation like `collections.create(...)`
2. `[Authorize]` ensures the user is authenticated
3. `[AbacRequirePermission]` runs next — calls `AbacService.RequirePermissionAsync` with no resource context
4. If denied → throws `FORBIDDEN` immediately, resolver never executes
5. If allowed → resolver proceeds normally

### Post-Resolver (`[UseAbacRowCheck]`)

1. User queries `apiKeys(where: { ... })` with paging/filtering/sorting
2. `[Authorize]` runs first — ensures user is authenticated
3. `[AbacRequirePermission]` runs — blanket ABAC check for `ApiKeys:Read`
4. Resolver returns `IQueryable<ApiKey>`
5. Filtering, sorting, and paging execute normally against the database
6. `[UseAbacRowCheck]` receives the materialized `Connection<ApiKey>`
7. For each node in the connection, it calls `AbacService.CheckPermissionAsync` with the row's resource data
8. If ALL rows pass → return the connection normally
9. If ANY row fails → throw `FORBIDDEN` immediately

## Future Optimization

The current implementation checks rows after materialization (post-paging). A future optimization would be to run a lightweight secondary query **before** paging executes:

```csharp
// Build inverse of ABAC ownership rule
var hasForbidden = await filteredQuery
    .Where(k => k.UserId != currentUserId)
    .Take(1)
    .AnyAsync();

if (hasForbidden) throw FORBIDDEN;
```

This requires intercepting the `IQueryable` between filtering and paging execution, which needs deeper Hot Chocolate middleware integration.

## Testing Notes

1. Create an ABAC policy: Allow Read on ApiKeys where `ResourceApiKeyUserId EqContextRef SubjectId`
2. Assign to a test user
3. Query `apiKeys()` → should return only their keys OR throw FORBIDDEN if the result set includes other keys
4. Query `apiKeys(where: { userId: { eq: "their-id" } })` → should pass because the filter is already restrictive
5. Query `myKeys` → should always return only their keys

## Migration Path for Existing Code

### Mutations with redundant blanket ABAC calls
Replace `[Authorize(Policy = "X:Y")]` + manual `RequirePermissionAsync` with `[Authorize]` + `[AbacRequirePermission]`:

```csharp
// Before (CollectionMutations.Create)
[Authorize(Policy = "Collections:Create")]
public async Task<Collection> Create(...)
{
    var userId = GetUserId(httpContextAccessor);
    await abacService.RequirePermissionAsync(userId, BaseResource.Collections, PermissionAction.Create);
    // ...
}

// After
[Authorize]
[AbacRequirePermission(BaseResource.Collections, PermissionAction.Create)]
public async Task<Collection> Create(...)
{
    // ...
}
```

### Mutations with resource-specific ABAC calls
Keep the manual call inside the resolver (the attribute cannot fetch the entity and build context):

```csharp
// CollectionMutations.Update — keep as-is
[Authorize(Policy = "Collections:Update")]
public async Task<Collection> Update(...)
{
    var userId = GetUserId(httpContextAccessor);
    var collection = await db.Collections.FindAsync(input.Id);
    // ...
    await abacService.RequirePermissionAsync(userId, BaseResource.Collections, PermissionAction.Update, collectionContext);
    // ...
}
```

### List queries
Add both attributes:

```csharp
[Authorize]
[AbacRequirePermission(BaseResource.ApiKeys, PermissionAction.Read)]
[UseAbacRowCheck(BaseResource.ApiKeys, PermissionAction.Read)]
[UsePaging(MaxPageSize = 100)]
[UseFiltering]
[UseSorting]
public IQueryable<ApiKey> ApiKeys(...)
```

## Rollout Order

1. Create both attribute classes
2. Add `ExtractResourceData` to `AbacService`
3. Apply `[AbacRequirePermission]` to mutations that have redundant blanket checks
4. Apply `[UseAbacRowCheck]` to `ApiKeyQueries.ApiKeys()` as the pilot
5. Add `myKeys` query
6. Test with "own keys only" ABAC policy
7. Roll out to other list queries (`Users`, `Assets`, `Collections`, etc.)

## Open Questions

- Should `UseAbacRowCheck` also be applied to `UserQueries.Users()`, `AssetQueries.Assets()`, and other list queries? (Yes, follow same pattern — extract resource data for each entity type in `AbacService.ExtractResourceData`)
- Should there be a fast-path that skips per-row checks if the user is an admin/has broad policies? (Already handled: `CheckPermissionAsync` uses cache and returns quickly for unrestricted users)
- Should `[AbacRequirePermission]` also replace `[Authorize(Policy = "X:Y")]` entirely, or should both coexist? (Recommendation: keep `[Authorize]` for authN gate, use `[AbacRequirePermission]` for blanket authZ gate)
