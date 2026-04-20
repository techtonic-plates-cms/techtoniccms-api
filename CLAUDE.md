# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What This Is

Headless CMS API built on **.NET 10** with **Hot Chocolate GraphQL**, **PostgreSQL** (EF Core), **Redis** sessions, and **S3-compatible** asset storage (RustFS). Authorization uses a custom **ABAC engine** (deny-first, priority-based policies with 30+ context attributes).

## Commands

```bash
# Build
dotnet build

# Run all infrastructure + app via Docker
docker compose -f compose.dev.yaml up -d

# EF Core migrations
dotnet tool restore
dotnet ef migrations add <Name> --project TechtonicCmsApi
dotnet ef database update --project TechtonicCmsApi
```

No test suite exists. GraphQL endpoint: `http://localhost:5095/graphql`.

Config is loaded from environment variables (see `.dev.env`): `Database__*`, `Redis__*`, `S3__*`, `Jwt__*`, `Admin__*`.

## Architecture

```
Program.cs          → Startup, middleware, DI, GraphQL config
├── Contexts/       → TechtonicCmsDbContext (EF Core, PostgreSQL)
├── Schema/         → Entities & Enums (domain models)
├── Services/       → Business logic (Scoped, injected via [Service])
├── Security/       → ABAC handler, security headers middleware
└── Types/          → GraphQL schema (Query, Mutation, domain modules)
    ├── Assets/     → REST endpoints (/assets/upload, /assets/{id}) + GraphQL types
    ├── Auth/       → Login/refresh/logout (JWT RS256)
    ├── Users/      → User CRUD, role assignment
    ├── Roles/      → Role CRUD, policy assignment
    ├── Policies/   → ABAC policy CRUD, rule management
    ├── Collections/→ Content schemas + DynamicCollections type module
    ├── Entries/    → Content entries (schema-less JSON via JsonDocument)
    └── Fields/     → Field definitions per collection
```

**Startup sequence:** EF Core migrations run automatically → `AdminBootstrapService` seeds admin + 95 ABAC policies → app listens.

## GraphQL Type Pattern

Root types (`Query.cs`, `Mutation.cs`) are minimal partial classes. Domain modules extend them via `[ExtendObjectType]`:

```csharp
// Types/Users/UserQueries.cs
public class UserQuery { /* resolver methods */ }

[ExtendObjectType(nameof(Query))]
public static class UserQueries
{
    public static UserQuery Users() => new();
}
```

Services are injected via the `[Service]` attribute in resolver parameters.

## Code Conventions

- **Naming:** PascalCase for C# types/methods; camelCase for GraphQL fields (auto by Hot Chocolate); UPPERCASE for enum string values (`.ToString().ToUpperInvariant()`); `{Action}{Domain}Input` for input types; `{Domain}Dto` for DTOs.
- **Entity IDs:** Always serialized as strings: `public static string GetId([Parent] Entity e) => e.Id.ToString();`
- **DateTimes:** Always UTC ISO 8601: `entry.CreatedAt.ToUniversalTime().ToString("o")`
- **Pagination:** Use `[UsePaging(MaxPageSize = 100)]`, `[UseFiltering]`, `[UseSorting]` on list resolvers.

## Authorization

- `[Authorize]` — requires authentication
- `[Authorize(Policy = "Resource:Action")]` — ABAC check (e.g., `"Users:Create"`)
- `[AllowAnonymous]` — login/refresh endpoints only
- Inside resolvers: `await abacService.RequirePermissionAsync(userId, resource, action)` throws `FORBIDDEN` on denial

## Error Handling

```csharp
throw new GraphQLException(ErrorBuilder.New()
    .SetMessage("Descriptive message")
    .SetCode("NOT_FOUND")  // NOT_FOUND | CONFLICT | BAD_REQUEST | UNAUTHENTICATED | FORBIDDEN | INVALID_ENUM
    .Build());
```

## ABAC Authorization Engine

Implemented in [Services/AbacService.cs](TechtonicCmsApi/Services/AbacService.cs). Every permission check follows this flow:

1. **Cache lookup** — `AbacEvaluationCache` table keyed by `(userId, resourceType, resourceId, action, fieldId?)`. Cache TTL: Allow = 5 min, Deny = 2 min.
2. **Policy resolution** — load active policies for the user via `UserPolicies` (direct) and `UserRoles → RolePolicies` (role-inherited). Filter by `ResourceType` + `ActionType`.
3. **Deny-first evaluation** — Deny policies sorted by descending priority, each evaluated against a context dictionary. First match = immediate `false` + audit write.
4. **Allow evaluation** — Allow policies sorted by descending priority. First match = `true` + audit write.
5. **Default deny** — if no Allow policy matches, return `false`.

**Policy rules** (`AbacPolicyRule`) apply predicates against a context dictionary built from `resourceData` passed by the resolver. Each rule specifies:

| Field | Options |
|-------|---------|
| `AttributePath` | `SubjectId`, `SubjectRole`, `SubjectStatus`, `ResourceEntryCreatedBy`, `ResourceAssetUploadedBy`, `EnvironmentCurrentTime`, `ActionType`, and more — see [Enums/AttributePath.cs](TechtonicCmsApi/Schema/TechtonicCms/Enums/AttributePath.cs) |
| `OperatorType` | `Eq`, `Ne`, `In`, `NotIn`, `Gt/Gte/Lt/Lte`, `Contains`, `StartsWith`, `EndsWith`, `Regex`, `IsNull`, `IsNotNull`, `EqContextRef` |
| `ValueType` | `String`, `Number`, `Boolean`, `Uuid`, `Datetime` |
| `RuleConnector` | `And` (all rules must match) or `Or` (any rule matches) |

`EqContextRef` is special: `ExpectedValue` is treated as another context key — used for ownership checks like `ResourceEntryCreatedBy == SubjectId`.

Every evaluation writes to `AbacAudit` (fire-and-forget, failures silently swallowed). Cache entries also store `PolicyVersions` (a comma-joined `id:updatedAt` string) for future invalidation.

**Calling from resolvers:**
```csharp
// Throws FORBIDDEN on denial
await abacService.RequirePermissionAsync(userId, BaseResource.Entries, PermissionAction.Update,
    new Dictionary<string, object?> {
        ["ResourceEntryCreatedBy"] = entry.CreatedBy.ToString(),
        ["ResourceEntryStatus"]    = entry.Status.ToString()
    });

// Returns bool (no throw)
var allowed = await abacService.CheckPermissionAsync(userId, BaseResource.Assets, PermissionAction.Read);
```

## Dynamic Schema (CollectionTypeModule)

[Types/Collections/DynamicCollections/CollectionTypeModule.cs](TechtonicCmsApi/Types/Collections/DynamicCollections/CollectionTypeModule.cs) is a Hot Chocolate `TypeModule` (Singleton) that reads `Collection` + `Field` rows at startup and emits runtime GraphQL types. No code generation — type definitions are built programmatically.

For each `Collection` with slug `blog-post` (PascalCase → `BlogPost`) the module creates:

| Generated type | Description |
|----------------|-------------|
| `BlogPostEntry` | Object type with static fields (`id`, `name`, `slug`, `status`, timestamps) + a `data: BlogPostEntryData!` field |
| `BlogPostEntryData` | Object type with one field per `Field` row; scalar fields resolve from `Entry.Data` (JSONB); relation fields async-resolve from `EntryRelation` |
| `BlogPostCreateEntryDataInput` | Required fields marked `!` based on `Field.IsRequired` |
| `BlogPostUpdateEntryDataInput` | All fields optional (partial update semantics) |

Filter and sort descriptors are also generated per field — scalar fields use `CmsDbFunctions.CmsExtract*` EF Core function mappings to JSONB operators; relation fields filter/sort via `Entry.FromRelations` subqueries.

**Refreshing types at runtime:** call `collectionTypeModule.TriggerTypesChanged()` after mutating collections or fields so Hot Chocolate rebuilds its schema. The collection mutations already do this.

**Relation resolution:** relation field resolvers receive the parent entry ID via a `__entryId` key injected into the data dictionary, then query `EntryRelations` filtered by `(entryId, fieldId)`.

## Other Key Patterns

- **Session + JWT separation:** Redis stores sessions (15 min TTL) and refresh tokens (7 day TTL). JWTs are stateless RS256 — sessions exist solely for revocation capability.
- **Password migration:** `PasswordService` transparently upgrades SHA256 → BCrypt on login (legacy accounts upgrade on first successful login).
- **Admin bootstrap:** `AdminBootstrapService` seeds admin user + 95 default ABAC policies (5 resources × 19 actions) idempotently on startup.
- **Multi-locale:** Collections and entries support localization via `Locale` enum (11 languages).
