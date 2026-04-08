---
description: "Use when creating or modifying GraphQL types, queries, mutations, resolvers, or input types in the Types/ directory. Covers the Hot Chocolate type module pattern, naming conventions, authorization annotations, and resolver boilerplate."
applyTo: "**/Types/**"
---

# GraphQL Type Layer Guidelines

## File Structure per Domain Module

Every domain lives under `Types/{DomainName}/` with up to 3 files:

| File | Purpose |
|------|---------|
| `{Domain}Queries.cs` | Query resolvers + `[ExtendObjectType(nameof(Query))]` |
| `{Domain}Mutations.cs` | Mutation resolvers, input classes + `[ExtendObjectType(nameof(Mutation))]` |
| `{Domain}Types.cs` | `[ObjectType<T>]` resolvers, DTOs for nested references |

## Query/Mutation Extension Pattern

```csharp
namespace TechtonicCmsApi.Types.{Domain};

public class {Domain}Query
{
    // Instance methods — resolvers go here
}

[ExtendObjectType(nameof(Query))]
public static class {Domain}Queries
{
    public static {Domain}Query {DomainPlural}() => new();
}
```

Same pattern for mutations with `Mutation` / `{Domain}Mutations`.

## Resolver Conventions

- **Services**: Inject via `[Service] TechtonicCmsDbContext db`, `[Service] AbacService abacService`, etc.
- **User context**: Extract current user ID with a private `GetUserId(IHttpContextAccessor)` helper parsing the `"userId"` claim.
- **ABAC checks**: Call `await abacService.RequirePermissionAsync(userId, BaseResource.{Resource}, PermissionAction.{Action})` at the top of every resolver (after `[Authorize]` gate).
- **Single-entity lookup**: Accept optional `Guid? id` and/or `string? name`/`slug`, throw `BAD_REQUEST` if neither provided.
- **List queries**: Add `[UsePaging(MaxPageSize = 100)]`, `[UseFiltering]`, `[UseSorting]` attributes. Support optional `string? search`, enum filters, `int? limit`, `int? offset`.

## Authorization Annotations

```csharp
[Authorize]                           // Any authenticated user
[Authorize(Policy = "Users:Create")]  // ABAC policy check (Resource:Action)
[AllowAnonymous]                      // Login/refresh only
```

## Input Types

Defined in the same file as mutations. Naming: `{Action}{Domain}Input`.

```csharp
public class Create{Domain}Input
{
    [GraphQLType<NonNullType<StringType>>]
    public string Name { get; set; } = "";

    [GraphQLType<NonNullType<IdType>>]
    public Guid Id { get; set; }
}
```

## Object Type Resolvers

Use `[ObjectType<Entity>]` on a `static partial class`. Key rules:

- **IDs as strings**: `public static string GetId([Parent] Entity e) => e.Id.ToString();`
- **Enums uppercase**: `public static string GetStatus([Parent] Entity e) => e.Status.ToString().ToUpperInvariant();`
- **DateTimes ISO 8601**: `entry.CreatedAt.ToUniversalTime().ToString("o")` — always UTC, always `"o"` format.
- **Nullable DateTimes**: Return `string?` and null-check before formatting.
- **Navigation properties**: Use `[Service] TechtonicCmsDbContext db` + async resolver with `await db.Related.Where(...)`.

## DTOs for Nested References

Place reference DTOs in the types file. Naming: `{Domain}RefDto` or `{Domain}RefIn{Parent}Dto`.

```csharp
public class RoleRefDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public DateTime? AssignedAt { get; set; }
}
```

Each DTO gets its own `[ObjectType<Dto>]` partial class with ID-to-string resolvers.

## Error Codes

Always uppercase: `NOT_FOUND`, `CONFLICT`, `BAD_REQUEST`, `UNAUTHENTICATED`, `FORBIDDEN`, `INVALID_ENUM`.

```csharp
throw new GraphQLException(ErrorBuilder.New()
    .SetMessage("Descriptive message")
    .SetCode("NOT_FOUND")
    .Build());
```

## Registering New Types

New domain types are auto-discovered by Hot Chocolate if the namespace is correct. For dynamic types (collections), update `CollectionTypeModule`.
