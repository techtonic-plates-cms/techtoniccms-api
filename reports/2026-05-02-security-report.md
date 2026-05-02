# TechtonicCMS Security Audit Report

**Date:** 2026-05-02
**Scope:** AuthN/AuthZ, ABAC, Data Protection, GraphQL/API, Infrastructure
**Auditor:** security-report agent

---

## Executive Summary

| Metric | Value |
|--------|-------|
| Risk Score | 52 |
| Critical | 0 |
| High | 3 |
| Medium | 8 |
| Low | 6 |
| Total Findings | 17 |

This audit identified **3 new High-severity** issues involving privilege escalation and information disclosure, alongside **8 Medium** and **6 Low** severity findings. Several issues from the prior audit (2026-04-27) remain unresolved and are included below. The most significant new risks are unprotected role-assignment mutations that allow any user with `Users:Update` to escalate privileges, and an unauthenticated endpoint that exposes full system documentation.

---

## Methodology

This audit was performed by a dedicated security subagent using tool-driven codebase analysis. The following phases were executed:

1. **Reconnaissance** — Mapped middleware, DI services, and configuration files
2. **Authentication & Sessions** — Reviewed JWT, session, password, and API key handling
3. **ABAC Authorization** — Analyzed policy evaluation, caching, and audit logging
4. **Data Protection & GraphQL** — Inspected asset handling, security headers, and GraphQL configuration
5. **Infrastructure** — Evaluated Docker Compose, environment variables, and network exposure

---

## Findings

### Critical

No critical findings were identified.

---

### High

#### Privilege Escalation via Unprotected Role/Policy Assignment

- **Category:** AuthZ
- **Location:** `UserMutations.cs:355`, `UserMutations.cs:403`, `UserMutations.cs:425`, `UserMutations.cs:481`, `UserMutations.cs:499`, `UserMutations.cs:523`
- **Effort:** Small

**Evidence:**
```csharp
// UserMutations.cs:355
[Authorize("Users:Update")]
public async Task<bool> AssignRole(AssignRoleInput input, ...)
{
    // No abacService.RequirePermissionAsync call
    db.UserRoles.Add(new UserRole { UserId = input.UserId, RoleId = input.RoleId, ... });
    ...
}
```

**Impact:** Any authenticated user who holds the blanket `Users:Update` permission can assign arbitrary roles (including the admin role) to themselves or any other user, and can assign arbitrary ABAC policies. The injected `AbacService` is never consulted for these six mutation methods.

**Recommendation:** Add `abacService.RequirePermissionAsync(currentUserId, BaseResource.Users, PermissionAction.Update, ...)` before modifying roles/policies, and validate that the caller is authorized to grant the specific target role.

---

#### API Key Creation for Arbitrary Users

- **Category:** AuthZ
- **Location:** `ApiKeyMutations.cs:38`
- **Effort:** Small

**Evidence:**
```csharp
// ApiKeyMutations.cs:38
[Authorize("ApiKeys:Create")]
public async Task<CreateApiKeyPayload> CreateApiKey(CreateApiKeyInput input, ...)
{
    var targetUserId = input.UserId ?? currentUserId;
    if (input.UserId.HasValue && input.UserId.Value != currentUserId)
    {
        var targetUser = await db.Users.FindAsync(targetUserId);
        if (targetUser is null) throw ...;
    }
    // Key is created for targetUserId without any authorization check
}
```

**Impact:** A user with `ApiKeys:Create` can create API keys for any other user (including administrators) and impersonate them. There is no row-level ABAC check verifying that the caller is allowed to manage keys on behalf of the target user.

**Recommendation:** Add `abacService.RequirePermissionAsync(currentUserId, BaseResource.ApiKeys, PermissionAction.Create, new() { ["ResourceApiKeyUserId"] = targetUserId.ToString() })` before creating the key.

---

#### Unauthenticated System Documentation Endpoint

- **Category:** Data Protection
- **Location:** `LlmsEndpoints.cs:17`
- **Effort:** Small

**Evidence:**
```csharp
// LlmsEndpoints.cs:17
app.MapGet("/llms.md", async (IDbContextFactory<TechtonicCmsDbContext> dbFactory, ...) =>
{
    var markdown = BuildMarkdown(collections);
    return Results.Text(markdown, "text/markdown; charset=utf-8");
})
.RequireRateLimiting("GeneralApi");
```

**Impact:** When `Llms:EndpointEnabled` is `true`, the `/llms.md` endpoint is publicly accessible without authentication. It returns comprehensive system documentation including authentication flows, ABAC internals, GraphQL schema details, and REST endpoint descriptions. This provides an attacker with a complete reconnaissance map of the system.

**Recommendation:** Add `.RequireAuthorization()` to the endpoint, or gate it behind an API key or admin-only ABAC check.

---

### Medium

#### Asset List Query Pre-Filter Bypasses ABAC Row Check

- **Category:** AuthZ
- **Location:** `AssetQueries.cs:58`
- **Effort:** Small

**Evidence:**
```csharp
// AssetQueries.cs:58
IQueryable<Asset> query = db.Assets.Where(a => a.IsPublic || a.UploadedBy == userId);
```

**Impact:** The `Assets` list resolver pre-filters results to public assets or assets owned by the caller before the `[UseAbacRowCheck]` middleware executes. This makes the row-level check a no-op and prevents administrators (who may have blanket `Assets:Read` via ABAC) from listing all assets through this query.

**Recommendation:** Remove the hardcoded pre-filter and let ABAC row checks enforce access control. If public assets must be visible to unauthenticated users, expose them through a separate unauthenticated query.

---

#### Audit Record Query Lacks Row-Level Authorization

- **Category:** AuthZ
- **Location:** `AuditQueries.cs:23`
- **Effort:** Small

**Evidence:**
```csharp
// AuditQueries.cs:23
[Authorize(Policy = "Audits:Read")]
public AbacAudit? Audit(Guid id, ...)
{
    var audit = db.AbacAudits.Find(id);
    return audit;
}
```

**Impact:** The single-record audit resolver only checks the blanket `Audits:Read` policy. If future ABAC policies restrict audit visibility (e.g., users can only view their own audits), this resolver bypasses those restrictions.

**Recommendation:** Add `abacService.RequirePermissionAsync` with resource data containing the audit's user ID before returning the record.

---

#### User Creation Allows Arbitrary Role Assignment

- **Category:** AuthZ
- **Location:** `UserMutations.cs:112`
- **Effort:** Medium

**Evidence:**
```csharp
// UserMutations.cs:112
[Authorize("Users:Create")]
public async Task<UserEntity> Create(CreateUserInput input, ...)
{
    if (input.Roles?.Ids is { Length: > 0 })
    {
        foreach (var roleId in input.Roles.Ids)
        {
            db.UserRoles.Add(new UserRole { ..., RoleId = roleId });
        }
    }
    ...
}
```

**Impact:** A user with `Users:Create` can create a new user and immediately assign any role (including admin) without additional authorization checks. This is a privilege escalation path if `Users:Create` is ever delegated to non-administrators.

**Recommendation:** Validate that the caller is authorized to assign each requested role, or restrict role assignment to a separate mutation that performs additional authorization.

---

#### Dynamic Entry Queries Lack Row-Level Ownership Checks

- **Category:** AuthZ
- **Location:** `CollectionTypeModuleQueries.cs:111`
- **Effort:** Medium

**Evidence:**
```csharp
// CollectionTypeModuleQueries.cs:111
await readAbacService.RequirePermissionAsync(
    readUserId,
    BaseResource.Entries,
    PermissionAction.Read,
    new Dictionary<string, object?>
    {
        ["ResourceEntryCollectionId"] = collectionId.ToString()
    });
```

**Impact:** Generated per-collection entry queries only validate blanket collection-level read permission. Unlike collections and API keys, no creator-based ownership policies are seeded for entries, and no `[UseAbacRowCheck]` is applied. If a user has `Entries:Read` for a collection, they can read all entries in it.

**Recommendation:** Add creator-based entry policies to `AdminBootstrapService` and apply `[UseAbacRowCheck]` to dynamically generated entry list fields.

---

#### GraphQL Introspection Not Explicitly Disabled in Production

- **Category:** GraphQL/API
- **Location:** `Program.cs:160`
- **Effort:** Small

**Evidence:**
```csharp
// Program.cs:160
.AddGraphQL()
.ModifyRequestOptions(options =>
{
    options.IncludeExceptionDetails = builder.Environment.IsDevelopment();
})
```

**Impact:** Hot Chocolate enables schema introspection by default. In production, introspection may remain active, exposing the full GraphQL schema to unauthenticated attackers and aiding reconnaissance.

**Recommendation:** Explicitly disable introspection in production: `.ModifyRequestOptions(o => { o.EnableIntrospection = builder.Environment.IsDevelopment(); })`.

---

#### No BANNED User Status

- **Category:** AuthN
- **Location:** `UserStatus.cs:6`
- **Effort:** Small

**Evidence:**
```csharp
// UserStatus.cs:6
public enum UserStatus
{
    Active,
    Inactive
}
```

**Impact:** The system only distinguishes between Active and Inactive users. There is no `Banned` state that would allow differentiated enforcement (e.g., preserving data but permanently blocking login, with clear audit intent). This limits administrative options for account enforcement.

**Recommendation:** Add a `Banned` value to `UserStatus` and update authentication handlers to reject banned users with a distinct error code.

---

#### Development JWT TTL Override Still Present

- **Category:** AuthN
- **Location:** `Program.cs:41`
- **Effort:** Small

**Evidence:**
```csharp
// Program.cs:41
if (builder.Environment.IsDevelopment())
{
    builder.Configuration["Jwt:AccessTokenTtlMinutes"] = "1440"; // 1 day
}
```

**Impact:** Access tokens last 24 hours in development. If the application is accidentally deployed with `ASPNETCORE_ENVIRONMENT=Development`, session tokens have an excessive lifetime, increasing the blast radius of token theft. This issue was identified in the previous audit and remains unresolved.

**Recommendation:** Remove the code-gated override. Use `appsettings.Development.json` for local development configuration instead.

---

### Low

#### UseAbacRowCheck Information Leakage

- **Category:** AuthZ
- **Location:** `UseAbacRowCheckAttribute.cs:95`
- **Effort:** Medium

**Evidence:**
```csharp
// UseAbacRowCheckAttribute.cs:95
var hasForbidden = (bool)task.GetType().GetProperty("Result")!.GetValue(task)!;
if (hasForbidden)
{
    throw new GraphQLException(ErrorBuilder.New()
        .SetMessage("Access denied: you do not have permission to view some of the requested records")
        .SetCode("FORBIDDEN")
        .Build());
}
```

**Impact:** A user with ownership-restricting policies can probe for the existence of records they do not own by observing the `FORBIDDEN` response. While the specific record is not revealed, the attacker learns that at least one forbidden record exists in the queried result set.

**Recommendation:** Consider replacing the exception with silent filtering (return only authorized rows) rather than failing the entire request when mixed permissions exist.

---

#### No Password Strength Enforcement

- **Category:** AuthN
- **Location:** `UserMutations.cs:112`, `UserMutations.cs:305`
- **Effort:** Small

**Evidence:**
```csharp
// UserMutations.cs:112
var passwordHash = passwordService.HashPassword(input.Password);
// No length, complexity, or entropy checks
```

**Impact:** Administrators can create users with weak passwords (e.g., "123456"), and users can change their passwords to equally weak values. This undermines the strong Argon2 hashing by allowing trivial passwords.

**Recommendation:** Add password strength validation (minimum length, complexity requirements, or entropy check) before hashing.

---

#### Missing GraphQL Error Filter

- **Category:** GraphQL/API
- **Location:** `Program.cs:160`
- **Effort:** Small

**Evidence:** No `AddErrorFilter` call exists in the GraphQL server configuration.

**Impact:** While `IncludeExceptionDetails` is gated by `IsDevelopment()`, unexpected internal exceptions may still leak implementation details through Hot Chocolate's default error formatting in production.

**Recommendation:** Register an error filter (`AddErrorFilter`) to sanitize error messages and strip stack traces in production.

---

#### Missing Security Headers (HSTS, CSP)

- **Category:** Infrastructure
- **Location:** `SecurityHeadersMiddleware.cs`
- **Effort:** Small

**Impact:** Missing `Strict-Transport-Security` allows SSL stripping attacks. Missing `Content-Security-Policy` reduces protection against XSS and data injection attacks. This issue was identified in the previous audit and remains unresolved.

**Recommendation:** Add `Strict-Transport-Security: max-age=31536000; includeSubDomains; preload` and a restrictive `Content-Security-Policy` header appropriate for a GraphQL API.

---

#### Deprecated X-XSS-Protection Header

- **Category:** Infrastructure
- **Location:** `SecurityHeadersMiddleware.cs:16`
- **Effort:** Small

**Impact:** The `X-XSS-Protection` header is deprecated and has been shown to introduce XSS vulnerabilities in some older browsers. Modern best practice is to rely on CSP. This issue was identified in the previous audit and remains unresolved.

**Recommendation:** Remove the `X-XSS-Protection` header.

---

#### Asset Delete Catches All Exceptions Silently

- **Category:** Data Protection
- **Location:** `AssetMutations.cs:82`
- **Effort:** Small

**Impact:** S3 deletion failures are silently ignored. The database record is removed but the underlying file may remain in storage, causing orphaned files and potential data retention policy violations. This issue was identified in the previous audit and remains unresolved.

**Recommendation:** Log the exception with `ILogger` and consider returning a warning to the caller or scheduling a background cleanup job.

---

## Recommendations (Prioritized)

1. **Fix privilege escalation in role/policy assignment** (Small) — Add `RequirePermissionAsync` with target user resource data to `AssignRole`, `UnassignRole`, `AssignPolicy`, `UnassignPolicy`, `UpdateRoleExpiration`, and `UpdatePolicyExpiration`.
2. **Restrict API key creation to authorized targets** (Small) — Add ABAC resource check in `CreateApiKey` before creating keys for other users.
3. **Require authentication on `/llms.md`** (Small) — Add `.RequireAuthorization()` or an admin ABAC check.
4. **Remove asset list pre-filter** (Small) — Let ABAC row checks handle access control instead of hardcoded `Where(a => a.IsPublic || a.UploadedBy == userId)`.
5. **Add row-level check to Audit query** (Small) — Call `RequirePermissionAsync` in `AuditQueries.Audit`.
6. **Validate roles on user creation** (Medium) — Ensure the creator is authorized to assign each requested role.
7. **Add entry ownership policies** (Medium) — Seed creator-based entry policies and apply row checks to dynamic entry queries.
8. **Disable GraphQL introspection in production** (Small) — Gate `EnableIntrospection` behind `IsDevelopment()`.
9. **Add BANNED user status** (Small) — Extend `UserStatus` enum and update auth handlers.
10. **Remove development JWT TTL override** (Small) — Move local dev config to `appsettings.Development.json`.
11. **Enforce password strength** (Small) — Add minimum length and complexity rules.
12. **Add GraphQL error filter** (Small) — Sanitize production error responses.
13. **Add missing security headers** (Small) — Include HSTS and CSP.
14. **Remove deprecated X-XSS-Protection header** (Small).
15. **Log S3 deletion failures** (Small) — Replace empty catch with structured logging.

---

## Appendix

### Scanned Files

- `Program.cs`
- `TechtonicCmsApi/Services/AuthService.cs`
- `TechtonicCmsApi/Services/SessionService.cs`
- `TechtonicCmsApi/Services/PasswordService.cs`
- `TechtonicCmsApi/Services/ApiKeyService.cs`
- `TechtonicCmsApi/Services/AbacService.cs`
- `TechtonicCmsApi/Services/AdminBootstrapService.cs`
- `TechtonicCmsApi/Services/S3Service.cs`
- `TechtonicCmsApi/Services/SchedulerService.cs`
- `TechtonicCmsApi/Security/AbacAuthorizationHandler.cs`
- `TechtonicCmsApi/Security/ApiKeyAuthenticationHandler.cs`
- `TechtonicCmsApi/Security/SecurityHeadersMiddleware.cs`
- `TechtonicCmsApi/Security/UseAbacRowCheckAttribute.cs`
- `TechtonicCmsApi/Security/AbacRequirePermissionAttribute.cs`
- `TechtonicCmsApi/Types/Auth/AuthMutations.cs`
- `TechtonicCmsApi/Types/Auth/AuthQueries.cs`
- `TechtonicCmsApi/Types/Users/UserQueries.cs`
- `TechtonicCmsApi/Types/Users/UserMutations.cs`
- `TechtonicCmsApi/Types/Assets/AssetQueries.cs`
- `TechtonicCmsApi/Types/Assets/AssetMutations.cs`
- `TechtonicCmsApi/Types/Assets/AssetEndpoints.cs`
- `TechtonicCmsApi/Types/ApiKeys/ApiKeyQueries.cs`
- `TechtonicCmsApi/Types/ApiKeys/ApiKeyMutations.cs`
- `TechtonicCmsApi/Types/Collections/CollectionQueries.cs`
- `TechtonicCmsApi/Types/Collections/CollectionMutations.cs`
- `TechtonicCmsApi/Types/Collections/DynamicCollections/CollectionTypeModuleQueries.cs`
- `TechtonicCmsApi/Types/Collections/DynamicCollections/CollectionTypeModuleMutations.cs`
- `TechtonicCmsApi/Types/Audit/AuditQueries.cs`
- `TechtonicCmsApi/Types/Llms/LlmsEndpoints.cs`
- `TechtonicCmsApi/Schema/TechtonicCms/Enums/UserStatus.cs`
- `TechtonicCmsApi/Schema/TechtonicCms/Enums/PermissionAction.cs`
- `TechtonicCmsApi/Schema/TechtonicCms/Enums/BaseResource.cs`
- `TechtonicCmsApi/Schema/TechtonicCms/Enums/AttributePath.cs`
- `compose.dev.yaml`
- `containers/Containerfile`
- `.dev.env`
