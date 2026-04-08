---
description: "Scaffold a complete new domain module with entity, enum updates, GraphQL types, service, and ABAC integration"
name: "Add Domain Module"
argument-hint: "<DomainName> (e.g., Comments, Tags, Webhooks)"
---

# Add Domain Module

Scaffold a full domain module for **$ARGUMENTS** following TechtonicCMS conventions.

## Steps

### 1. Analyze Requirements

Before writing code, identify:
- What entity fields the domain needs (ID, timestamps, foreign keys)
- Which `BaseResource` and `PermissionAction` enum values to add (if any)
- Relationships to existing entities (User, Collection, Entry, Asset)
- Whether multi-locale support is needed (see `Locale` enum)

### 2. Create Entity

Create `Schema/TechtonicCms/Entities/{Domain}.cs`:

- Properties: `Id` (Guid), domain-specific fields, `CreatedBy` (Guid, FK to User), timestamps (DateTime)
- Use appropriate C# types; JSON fields use `JsonDocument`
- Follow existing entity patterns (see [User.cs](../TechtonicCmsApi/Schema/TechtonicCms/Entities/User.cs), [Entry.cs](../TechtonicCmsApi/Schema/TechtonicCms/Entities/Entry.cs))

### 3. Update Enums (if needed)

In `Schema/TechtonicCms/Enums/`:
- Add new value to `BaseResource` if this is a new resource type
- Add new values to `PermissionAction` for any new actions
- Add any domain-specific enums

### 4. Update DbContext

In [Contexts/TechtonicCmsDbContext.cs](../TechtonicCmsApi/Contexts/TechtonicCmsDbContext.cs):
- Add `DbSet<{Domain}>` property
- Add entity configuration in `OnModelCreating` (constraints, indexes, enum mappings)
- Add PostgreSQL enum mapping for any new enums: `builder.HasPostgresEnum<EnumType>()`

### 5. Create GraphQL Types

Create `Types/{Domain}/` with three files:

**{Domain}Types.cs** — `[ObjectType<Entity>]` with:
- `GetId` returning `entity.Id.ToString()`
- Enum fields as `.ToString().ToUpperInvariant()`
- DateTimes as `.ToUniversalTime().ToString("o")`
- Navigation property resolvers using `[Service] TechtonicCmsDbContext`

**{Domain}Queries.cs** — Query resolvers with:
- `[Authorize]` on all queries
- `abacService.RequirePermissionAsync()` runtime check
- Single-item lookup by `Guid? id` and/or unique string field
- List query with `[UsePaging(MaxPageSize = 100)]`, `[UseFiltering]`, `[UseSorting]`

**{Domain}Mutations.cs** — Mutation resolvers with:
- Input classes: `Create{Domain}Input`, `Update{Domain}Input` with `[GraphQLType<NonNullType<...>>]`
- `[Authorize(Policy = "{Resource}:{Action}")]` on each mutation
- `abacService.RequirePermissionAsync()` runtime check
- Proper error codes: `NOT_FOUND`, `CONFLICT`, `BAD_REQUEST`

### 6. Update ABAC Registration (if new resource)

In [Security/AbacAuthorizationHandler.cs](../TechtonicCmsApi/Security/AbacAuthorizationHandler.cs):
- Add new resource to `SecurityPolicies.Register()` loop

In [Services/AdminBootstrapService.cs](../TechtonicCmsApi/Services/AdminBootstrapService.cs):
- The seeding loop automatically covers new resources/actions (iterates `BaseResource` × `PermissionAction`)

### 7. Create Migration

```bash
dotnet ef migrations add Add{Domain} --project TechtonicCmsApi
```

Review the generated migration for correctness before proceeding.

### 8. Register in GraphQL

In [Program.cs](../TechtonicCmsApi/Program.cs), the `AddTypes()` call auto-discovers types in the `Types/` namespace. No manual registration needed unless adding a new service.

If a new service is needed, register it as Scoped:
```csharp
builder.Services.AddScoped<{Domain}Service>();
```

## Verification Checklist

- [ ] Entity has `Id` (Guid), timestamps, proper foreign keys
- [ ] DbContext has `DbSet` + configuration + enum mappings
- [ ] GraphQL types follow naming: `{Domain}Query`/`{Domain}Mutation` + `[ExtendObjectType]`
- [ ] All resolvers have `[Authorize]` + runtime ABAC check
- [ ] Input types use `[GraphQLType<NonNullType<...>>]` for required fields
- [ ] IDs returned as strings, enums as UPPERCASE, dates as ISO 8601
- [ ] Error codes are uppercase (`NOT_FOUND`, `CONFLICT`, etc.)
- [ ] Migration generated and reviewed
- [ ] `dotnet build` succeeds with no errors
