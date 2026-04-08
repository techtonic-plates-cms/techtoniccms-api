# TechtonicCMS API — Project Guidelines

## Architecture

Headless CMS API built on **.NET 10** with a **Hot Chocolate GraphQL** layer, **PostgreSQL** persistence (EF Core), **Redis** sessions, and **S3-compatible** asset storage (RustFS/MinIO). Authorization uses a custom **ABAC engine** (deny-first, priority-based policies with 30+ context attributes).

### Layer Structure

```
Program.cs          → Startup, middleware, DI, GraphQL config
├── Contexts/       → TechtonicCmsDbContext (EF Core, PostgreSQL enums)
├── Schema/         → Entities & Enums (domain models)
├── Services/       → Business logic (Scoped, injected via [Service])
├── Security/       → ABAC handler, security headers middleware
└── Types/          → GraphQL schema (Query, Mutation, domain modules)
    ├── Assets/     → REST endpoints + GraphQL types
    ├── Auth/       → Login/refresh/logout (JWT RS256)
    ├── Users/      → User CRUD, role assignment
    ├── Roles/      → Role CRUD, policy assignment
    ├── Policies/   → ABAC policy CRUD, rule management
    ├── Collections/→ Content schemas + DynamicCollections type module
    ├── Entries/    → Content entries (schema-less JSON data)
    └── Fields/     → Field definitions per collection
```

## Build & Run

```bash
# Restore and build
dotnet build

# Run (requires PostgreSQL, Redis, S3 — use Docker)
docker compose -f compose.dev.yaml up -d

# EF Core migrations
dotnet tool restore
dotnet ef migrations add <Name> --project TechtonicCmsApi
dotnet ef database update --project TechtonicCmsApi

# GraphQL endpoint
http://localhost:5095/graphql
```

### Infrastructure (compose.dev.yaml)

| Service | Image | Port |
|---------|-------|------|
| `dev` | .NET 10 SDK | 5095 |
| `database` | postgres:18.2 | 5432 |
| `redis` | redis:8.2.0-alpine | 6379 |
| `rustfs` | rustfs/rustfs:alpha | 9000 |

Config is loaded from environment variables (see `.dev.env`): `Database__*`, `Redis__*`, `S3__*`, `Jwt__*`, `Admin__*`.

## Code Conventions

### GraphQL Type Pattern

Root types (`Query.cs`, `Mutation.cs`) are minimal `[QueryType]`/`[MutationType]` partial classes. Domain modules extend them:

```csharp
// Types/Users/UserQueries.cs
public class UserQuery { /* resolver methods */ }

[ExtendObjectType(nameof(Query))]
public static class UserQueries
{
    public static UserQuery Users() => new();
}
```

### Naming

- **PascalCase**: C# types, methods, properties
- **camelCase**: GraphQL field names (auto by Hot Chocolate)
- **UPPERCASE**: Enum string values in responses (`.ToString().ToUpperInvariant()`)
- **Input types**: `{Action}{Domain}Input` (e.g., `CreateCollectionInput`)
- **DTOs**: `{Domain}Dto` or `{Domain}RefDto` for nested references

### Entity IDs

Always serialized as strings via explicit resolvers: `public static string GetId([Parent] Entity e) => e.Id.ToString();`

### Authorization

- `[Authorize]` — requires authentication
- `[Authorize(Policy = "Resource:Action")]` — ABAC policy check (e.g., `"Users:Create"`)
- `[AllowAnonymous]` — login/refresh endpoints only
- Runtime: `abacService.RequirePermissionAsync(userId, resource, action)` throws on denial

### Error Handling

```csharp
throw new GraphQLException(ErrorBuilder.New()
    .SetMessage("Descriptive message")
    .SetCode("NOT_FOUND")  // Uppercase: NOT_FOUND, CONFLICT, BAD_REQUEST, UNAUTHENTICATED, FORBIDDEN, INVALID_ENUM
    .Build());
```

### DateTimes

Always UTC, ISO 8601 roundtrip format: `entry.CreatedAt.ToUniversalTime().ToString("o")`

### Pagination

Use Hot Chocolate attributes on list queries:
```csharp
[UsePaging(MaxPageSize = 100)]
[UseFiltering]
[UseSorting]
```

### DI Service Access in GraphQL

Services injected via `[Service]` attribute, `DbContext` via `[Service]` with scoped lifetime.

## Key Patterns

- **Deny-first ABAC**: Deny policies evaluated first; any match = immediate denial. Allow policies checked by priority.
- **Dynamic collections**: `CollectionTypeModule` generates GraphQL types at runtime from DB collection definitions.
- **Session + JWT separation**: Redis stores sessions (15 min TTL) and refresh tokens (7 day TTL). JWTs are stateless with RS256.
- **Password migration**: `PasswordService` transparently upgrades SHA256 → BCrypt on login.
- **Admin bootstrap**: `AdminBootstrapService` seeds admin user + 95 policies (5 resources × 19 actions) on startup.
- **Multi-locale**: Collections/entries support localization via `Locale` enum (11 languages).
