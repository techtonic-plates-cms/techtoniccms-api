# How `UseAbacRowCheckAttribute` Works

**Date:** 2026-05-03  
**Subject:** Row-level ownership filtering middleware for Hot Chocolate GraphQL  
**File:** `TechtonicCmsApi/Security/UseAbacRowCheckAttribute.cs`

---

## 1. Overview

`UseAbacRowCheckAttribute` is a **Hot Chocolate `ObjectFieldDescriptorAttribute`** that injects **row-level ownership filtering** into list queries. It bridges the gap between coarse-grained ABAC authorization ("can this user read API keys?") and fine-grained data access ("which API keys can this user read?").

Unlike `[Authorize(Policy = "Resource:Action")]`, which performs a single yes/no check before the resolver runs, this attribute:
1. Lets the resolver return an `IQueryable<T>`
2. Probes the ABAC engine to detect if the user is restricted to owned resources
3. Injects a `WHERE ownershipField = currentUserId` clause into the query
4. Returns the filtered query for paging and materialization

The filter executes **in the database**, not in memory, avoiding N+1 problems and information leakage.

---

## 2. Where It Fits: The Middleware Pipeline

The attribute is applied to GraphQL field resolvers that return `IQueryable<T>`, alongside other Hot Chocolate attributes:

```csharp
[Authorize]                                    // 1. Authentication check
[UsePaging(MaxPageSize = 100)]                 // 2. Pagination middleware
[UseAbacRowCheck(BaseResource.ApiKeys, PermissionAction.Read)]  // 3. Ownership filter
[UseFiltering]                                 // 4. User-supplied filters
[UseSorting]                                   // 5. User-supplied sorting
public IQueryable<ApiKey> ApiKeys(...) => ...  // 6. Resolver
```

### Execution Order

Hot Chocolate middleware executes as a **stack**. The attribute calls `await next(ctx)` first, meaning:

1. **Before `next(ctx)`:** The middleware hasn't done anything yet
2. **`next(ctx)` executes:** The resolver runs, then `UseFiltering`, then `UseSorting` — each wrapping the next
3. **After `next(ctx)`:** The result is an `IQueryable<T>` with filters and sorting applied but **not yet executed against the database**
4. **The ownership `Where` clause is appended:** The query now has an extra predicate
5. **`UsePaging` materializes:** The final query (with ownership filter) is executed and paged

This ordering is critical: the ownership filter must be applied **after** user filters (so they compose correctly) but **before** paging (so page counts reflect only authorized rows).

---

## 3. The ABAC Probe Mechanism

The attribute does not hardcode rules like "if user is not admin, filter by owner." Instead, it **probes the ABAC engine** dynamically.

### 3.1 `IsRestrictedToOwnResourcesAsync`

```csharp
public async Task<bool> IsRestrictedToOwnResourcesAsync(
    Guid userId, BaseResource resource, PermissionAction action)
{
    var probeContext = CreateProbeContext(resource, Guid.NewGuid());
    if (probeContext.Count == 0)
        return false;

    var allowed = await CheckPermissionAsync(userId, resource, action, probeContext);
    return !allowed;
}
```

The method asks:

> *"If a resource were owned by a completely random stranger (a fresh `Guid.NewGuid()`), would you allow this user to access it?"*

If the answer is **deny**, it means the user's ABAC policies restrict them to their own resources → `isRestricted = true`.

### 3.2 `CreateProbeContext`

Each resource type has a synthetic context with a fake owner ID:

| Resource | Probe Context Keys |
|----------|-------------------|
| `ApiKeys` | `ResourceApiKeyUserId = randomGuid` |
| `Assets` | `ResourceAssetUploadedBy = randomGuid` |
| `Collections` | `ResourceCollectionCreatedBy = randomGuid`, `ResourceCollectionId`, `ResourceCollectionSlug`, `ResourceCollectionIsLocalized` |
| `Users` | `ResourceUserId = randomGuid`, `ResourceUserStatus = "Active"` |
| `Entries` | `ResourceEntryCreatedBy = randomGuid`, `ResourceEntryId`, `ResourceEntryStatus`, `ResourceEntryCollectionId`, `ResourceEntryLocale` |

These keys match the `AttributePath` enum values used in ABAC policy rules. The probe context is realistic enough that ownership-based policies will evaluate correctly.

### 3.3 Why a Probe?

The ABAC system supports complex policies with multiple rules, rule connectors (`And`/`Or`), operators (`Eq`, `In`, `Gt`, `Contains`, etc.), and context references. Rather than trying to reverse-engineer policies, the probe simply **asks the engine** what it would decide for a stranger-owned resource. This respects:

- Priority ordering
- Deny-first evaluation
- Role inheritance
- Policy expiration
- Custom rule combinations

---

## 4. Expression Tree Construction

If the probe returns `isRestricted = true`, the attribute builds a LINQ `Where` clause at runtime using expression trees.

### 4.1 Ownership Property Lookup

```csharp
public static string? GetOwnershipPropertyName(BaseResource resource) => resource switch
{
    BaseResource.ApiKeys     => "UserId",
    BaseResource.Assets      => "UploadedBy",
    BaseResource.Collections => "CreatedBy",
    BaseResource.Users       => "Id",
    BaseResource.Entries     => "CreatedBy",
    _ => null
};
```

This maps the abstract `BaseResource` to the concrete entity property that indicates ownership.

### 4.2 Building the Expression

```csharp
var entityType = queryable.ElementType;                    // e.g., typeof(ApiKey)
var param = Expression.Parameter(entityType, "x");          // x =>
var property = Expression.Property(param, ownershipProp);   // x.UserId

Expression allowedCondition;
if (property.Type == typeof(Guid))
    allowedCondition = Expression.Equal(property, Expression.Constant(userId));
else if (property.Type == typeof(string))
    allowedCondition = Expression.Equal(property, Expression.Constant(userId.ToString()));
else
    return; // Unsupported — bail out

var lambda = Expression.Lambda(allowedCondition, param);    // x => x.UserId == userId
```

### 4.3 Invoking `Queryable.Where`

Because the entity type is only known at runtime, reflection is used to invoke the generic `Queryable.Where<T>` method:

```csharp
var whereMethod = typeof(Queryable)
    .GetMethods()
    .First(m => m.Name == "Where" && m.GetParameters().Length == 2)
    .MakeGenericMethod(entityType);

var filteredQuery = whereMethod.Invoke(null, [queryable, lambda]);
ctx.Result = filteredQuery;
```

This produces an `IQueryable<T>` that EF Core will translate to SQL:

```sql
SELECT ... FROM "ApiKeys" a
WHERE a."UserId" = 'current-user-guid'
  AND /* other user filters */
ORDER BY /* user sorting */
LIMIT /* page size */ OFFSET /* page offset */
```

---

## 5. Step-by-Step Execution Flow

```
┌─────────────────────────────────────────────────────────────────────────────┐
│  GraphQL Request: query { apiKeys { nodes { id name } } }                   │
└─────────────────────────────────────────────────────────────────────────────┘
                                      │
                                      ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│  1. [Authorize] — JWT validation, extract userId claim                      │
└─────────────────────────────────────────────────────────────────────────────┘
                                      │
                                      ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│  2. [UsePaging] — wraps execution, will apply LIMIT/OFFSET at the end       │
└─────────────────────────────────────────────────────────────────────────────┘
                                      │
                                      ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│  3. [UseAbacRowCheck] — calls await next(ctx), passing control down         │
│     (does nothing yet)                                                      │
└─────────────────────────────────────────────────────────────────────────────┘
                                      │
                                      ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│  4. [UseFiltering] — appends WHERE clauses from GraphQL filter args         │
│  5. [UseSorting]  — appends ORDER BY from GraphQL sort args                 │
│  6. Resolver      — returns IQueryable<ApiKey> (unexecuted)                 │
└─────────────────────────────────────────────────────────────────────────────┘
                                      │
                                      ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│  7. [UseAbacRowCheck] — resumes after next(ctx):                           │
│     a. ctx.Result is IQueryable<ApiKey> ✓                                   │
│     b. Call IsRestrictedToOwnResourcesAsync(userId, ApiKeys, Read)          │
│        i.  Build probe context with random owner Guid                       │
│        ii. Call CheckPermissionAsync → ABAC evaluation                      │
│        iii. If denied → isRestricted = true                                 │
│     c. If restricted, build expression tree: x.UserId == userId             │
│     d. Invoke Queryable.Where via reflection                                │
│     e. Set ctx.Result = filtered IQueryable                                 │
└─────────────────────────────────────────────────────────────────────────────┘
                                      │
                                      ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│  8. [UsePaging] — executes the final SQL query with all predicates          │
│     Returns Connection<ApiKey> with only authorized rows                    │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## 6. Comparison: Dynamic Collection Approach

The dynamic collection module (`CollectionTypeModuleQueries.cs`) handles entry queries differently. Instead of using `UseAbacRowCheck`, it manually applies the ownership filter inline:

```csharp
var query = innerDb.Entries.Where(e => e.CollectionId == collectionId);

var isRestricted = await readAbacService.IsRestrictedToOwnResourcesAsync(
    readUserId, BaseResource.Entries, PermissionAction.Read);
if (isRestricted)
{
    query = query.Where(e => e.CreatedBy == readUserId);
}

return query.AsQueryable();
```

This is **functionally equivalent** to what `UseAbacRowCheck` does, but:
- It happens **inside the resolver** instead of in middleware
- It cannot be composed with `[UseFiltering]`/`[UseSorting]` in the same declarative way
- It requires explicit code for every dynamic field

`UseAbacRowCheck` is the preferred pattern for static resolvers because it separates concerns and is reusable.

---

## 7. Security Properties

### 7.1 Database-Side Filtering
The ownership predicate is translated to SQL by EF Core. Unauthorized rows are never fetched from the database, eliminating:
- **N+1 query problems** (no in-memory filtering)
- **Information leakage via page counts** (total count reflects only authorized rows)
- **Timing attacks** (no difference in response time based on forbidden row existence)

### 7.2 No Exception-Based Information Disclosure
An earlier design (mentioned in the security report) considered materializing results and throwing `FORBIDDEN` if any unauthorized rows were found. That approach would leak information: an attacker could infer record existence by observing `FORBIDDEN` vs. success responses.

The current implementation **silently filters** at the query level. The user receives a normal (possibly empty) result set with no indication that rows were excluded.

### 7.3 Probe Isolation
The probe uses a `Guid.NewGuid()` that is guaranteed not to collide with any real user ID. Even if cached, the cache key includes the resource ID (which is deterministic per resource type), so the probe result is cached per `(userId, resourceType, action)` — not per random GUID.

---

## 8. Current Usage and Gaps

### 8.1 Where It Is Applied

| Location | Resource | Action |
|----------|----------|--------|
| `ApiKeyQueries.ApiKeys()` | `ApiKeys` | `Read` |

### 8.2 Identified Gaps

Per the 2026-05-02 security report:

- **Entries:** Dynamically generated collection entry queries do not use `UseAbacRowCheck`. They have a manual inline filter in `CollectionTypeModuleQueries.cs`, but this is applied inconsistently.
- **Assets:** The `Assets` list resolver pre-filters to public/owned assets before `UseAbacRowCheck` runs, making the row-level check a no-op for the current query path.
- **Users, Collections, Roles, Policies:** No `[UseAbacRowCheck]` is applied to their list queries, though `GetOwnershipPropertyName` supports them.

### 8.3 Recommendations

1. Apply `[UseAbacRowCheck]` to all list resolvers returning user-scoped data
2. For dynamic entry fields, consider extracting the ownership filter into a shared helper or extending `CollectionTypeModuleQueries.cs` to use the attribute pattern
3. Add ownership-based policies to `AdminBootstrapService` for `Entries` so the probe mechanism has policies to evaluate against

---

## 9. Code Reference

### 9.1 Attribute Declaration
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
    // ...
}
```

### 9.2 Supported Ownership Property Types
- `Guid` — compared directly: `x.UserId == userId`
- `string` — compared as string: `x.UserId == "guid-string"`
- Other types — middleware bails out (no filter applied)

### 9.3 Key Dependencies
- `AbacService.IsRestrictedToOwnResourcesAsync()` — probes ABAC engine
- `AbacService.GetOwnershipPropertyName()` — maps resource to property
- `IHttpContextAccessor` — extracts authenticated user ID
- `System.Linq.Expressions` — builds runtime `Where` predicate

---

## 10. Summary

`UseAbacRowCheckAttribute` is a **declarative, middleware-driven row-level security filter** that:

1. **Probes** the ABAC engine with a synthetic stranger-owned resource to detect ownership restrictions
2. **Builds** a LINQ expression tree predicate at runtime matching the entity's ownership field to the current user
3. **Injects** the predicate into the `IQueryable` before database execution
4. **Returns** a filtered result set with no information leakage about excluded rows

It is the cleanest integration point for row-level security in the Hot Chocolate + EF Core stack, keeping authorization logic in middleware while the resolver remains focused on data projection.
