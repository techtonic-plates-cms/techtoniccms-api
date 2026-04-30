# Techtonic CMS API

Headless CMS API built on **.NET 10** with **Hot Chocolate GraphQL**, **PostgreSQL**, **Redis** sessions, and **S3-compatible** asset storage.

## Stack

- .NET 10 / ASP.NET Core
- Hot Chocolate (GraphQL)
- EF Core + PostgreSQL
- Redis (sessions & refresh tokens)
- S3-compatible storage (RustFS)
- ABAC authorization engine

## Quick Start

```bash
# Start infrastructure + app
docker compose -f compose.dev.yaml up -d

# Or run locally after starting postgres + redis
dotnet build
dotnet run --project TechtonicCmsApi
```

GraphQL endpoint: `http://localhost:5095/graphql`

## Project Structure

```
TechtonicCmsApi/
├── Contexts/       → EF Core DbContext
├── Schema/         → Domain entities & enums
├── Services/       → Business logic (ABAC, auth, etc.)
├── Security/       → ABAC handler, security middleware
└── Types/          → GraphQL schema modules
    ├── Assets/     → File upload & management
    ├── Auth/       → JWT login/refresh/logout
    ├── Users/      → User CRUD
    ├── Roles/      → Role & policy assignment
    ├── Policies/   → ABAC policy management
    ├── Collections/→ Content schemas (dynamic types)
    ├── Entries/    → Content entries (JSONB)
    └── Fields/     → Collection field definitions
```

## Configuration

Environment variables (see `.dev.env`):
- `Database__*` — PostgreSQL connection
- `Redis__*` — Redis connection
- `S3__*` — S3-compatible storage
- `Jwt__*` — RS256 key settings
- `Admin__*` — Bootstrap admin credentials

## Migrations

```bash
dotnet tool restore
dotnet ef migrations add <Name> --project TechtonicCmsApi
dotnet ef database update --project TechtonicCmsApi
```

Migrations run automatically on startup.
