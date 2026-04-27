# TechtonicCMS Security Audit Report

**Date:** 2026-04-27
**Scope:** AuthN/AuthZ, ABAC, Data Protection, GraphQL/API, Infrastructure
**Auditor:** security-report agent

---

## Executive Summary

| Metric | Value |
|--------|-------|
| Risk Score | 33 |
| Critical | 0 |
| High | 3 |
| Medium | 7 |
| Low | 4 |
| Total Findings | 14 |

This audit found **3 High** and **7 Medium** severity issues across authentication, authorization, and infrastructure layers. No Critical vulnerabilities were identified. The most significant risks are an ABAC policy bypass on Role/Policy queries, disabled JWT audience validation, and a hardcoded weak admin password fallback. All findings are actionable and most require small effort to remediate.

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

#### ABAC Bypass via Misconfigured Policies on Role and Policy Queries

- **Category:** AuthZ
- **Location:** `RoleQueries.cs:15`, `PolicyQueries.cs:16`
- **Effort:** Small

**Evidence:**
```csharp
// RoleQueries.cs:15
[Authorize(Policy = "Users:Read")]
public async Task<RoleEntity?> Role(...)

// PolicyQueries.cs:16
[Authorize(Policy = "Users:Read")]
public async Task<PolicyEntity?> Policy(...)
```

**Impact:** Any user who holds the `Users:Read` permission can enumerate all roles and policies in the system, bypassing the intended `Roles:Read` and `Policies:Read` ABAC controls. This undermines the principle of least privilege and may expose sensitive policy rules.

**Recommendation:** Change `RoleQueries.cs:15` to `[Authorize(Policy = "Roles:Read")]` and `PolicyQueries.cs:16` to `[Authorize(Policy = "Policies:Read")]`.

---

#### JWT Audience Validation Disabled

- **Category:** AuthN
- **Location:** `Program.cs:101`, `AuthService.cs:56`
- **Effort:** Small

**Evidence:**
```csharp
// Program.cs:101
ValidateAudience = false,

// AuthService.cs:56
ValidateAudience = false,
```

**Impact:** JWT access and refresh tokens are not validated against an intended audience. If the same RSA key pair is reused across multiple services, a token issued for one service could be replayed against the CMS API.

**Recommendation:** Set `ValidateAudience = true` and define `ValidAudience` (e.g., `"techtonic-cms-api"`) in both `Program.cs` and `AuthService.cs`.

---

#### Hardcoded Weak Admin Password Fallback

- **Category:** AuthN
- **Location:** `AdminBootstrapService.cs:16`
- **Effort:** Small

**Evidence:**
```csharp
var adminPassword = config["Admin:Password"] ?? "admin123";
```

**Impact:** If the `Admin:Password` environment variable is missing, the bootstrap service creates an admin account with the weak password `"admin123"`. An attacker can easily guess this and gain full administrative access.

**Recommendation:** Remove the fallback string. Throw an `InvalidOperationException` if `Admin:Password` is not configured, or generate a one-time cryptographically random password and log it securely.

---

### Medium

#### Development JWT TTL Override Creates Token Lifetime Risk

- **Category:** AuthN
- **Location:** `Program.cs:40-43`
- **Effort:** Small

**Evidence:**
```csharp
if (builder.Environment.IsDevelopment())
{
    builder.Configuration["Jwt:AccessTokenTtlMinutes"] = "1440"; // 1 day
}
```

**Impact:** Access tokens last 24 hours in development. If the application is accidentally deployed with `ASPNETCORE_ENVIRONMENT=Development`, session tokens have an excessive lifetime, increasing the blast radius of token theft.

**Recommendation:** Remove the code-gated override. Use `appsettings.Development.json` or `launchSettings.json` for local development configuration instead.

---

#### Refresh Token TTL Exceeds Stated Security Policy

- **Category:** AuthN
- **Location:** `SessionService.cs:14`, `AuthService.cs:16`
- **Effort:** Small

**Evidence:**
```csharp
// SessionService.cs:14
private static readonly TimeSpan RefreshTokenTtl = TimeSpan.FromDays(30);

// AuthService.cs:16
public int RefreshTokenTtlDays { get; set; } = 30;
```

**Impact:** Refresh tokens are valid for 30 days, exceeding the documented 7-day limit in the architecture guide. This extends the window of compromise if a refresh token is stolen.

**Recommendation:** Change the default to 7 days and ensure `Jwt:RefreshTokenTtlDays` environment variable is explicitly set in production.

---

#### Asset Download Endpoint Lacks Rate Limiting

- **Category:** GraphQL/API
- **Location:** `AssetEndpoints.cs:103`
- **Effort:** Small

**Evidence:**
```csharp
app.MapGet("/assets/{id:guid}", async (
    Guid id,
    HttpContext context,
    ...
) => {
    ...
    return Results.Stream(stream, asset.MimeType);
});
```

**Impact:** The endpoint does not have `.RequireRateLimiting()`. Unauthenticated users can hammer public asset downloads, causing bandwidth exhaustion or degraded availability.

**Recommendation:** Add `.RequireRateLimiting("GeneralApi")` or create a dedicated `"Download"` rate limiter with appropriate limits.

---

#### Missing Security Headers (HSTS, CSP)

- **Category:** Infrastructure
- **Location:** `SecurityHeadersMiddleware.cs`
- **Effort:** Small

**Evidence:**
```csharp
context.Response.Headers["X-Content-Type-Options"] = "nosniff";
context.Response.Headers["X-Frame-Options"] = "DENY";
context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
```

**Impact:** Missing `Strict-Transport-Security` allows SSL stripping attacks. Missing `Content-Security-Policy` reduces protection against XSS and data injection attacks.

**Recommendation:** Add `Strict-Transport-Security: max-age=31536000; includeSubDomains; preload` and a restrictive `Content-Security-Policy` header appropriate for a GraphQL API.

---

#### No CORS Configuration

- **Category:** Infrastructure
- **Location:** `Program.cs`
- **Effort:** Small

**Evidence:** No `AddCors`, `UseCors`, or CORS policy registration exists in `Program.cs`.

**Impact:** Without an explicit CORS policy, browser-based clients may be subject to default browser behavior or overly permissive reverse proxy settings, increasing the risk of cross-origin attacks.

**Recommendation:** Register an explicit CORS policy with an allowlist of trusted origins. Do not use `AllowAnyOrigin` with credentials.

---

#### REST Endpoint Throws GraphQLException

- **Category:** GraphQL/API
- **Location:** `AssetEndpoints.cs:38`, `AssetEndpoints.cs:46`, `AssetEndpoints.cs:74`
- **Effort:** Small

**Evidence:**
```csharp
throw new GraphQLException(ErrorBuilder.New()
    .SetMessage("File exceeds maximum size of 50MB")
    .SetCode("BAD_REQUEST")
    .Build());
```

**Impact:** A REST endpoint throwing `GraphQLException` violates HTTP semantics and may leak GraphQL-specific error structures through the REST pipeline. Hot Chocolate's error handling may not integrate cleanly with ASP.NET Core's REST exception handling.

**Recommendation:** Use `Results.BadRequest(...)`, `Results.Problem(...)`, or standard ASP.NET Core middleware exception handling for REST endpoints.

---

#### S3 Presigned URL Default TTL Not Bounded

- **Category:** Data Protection
- **Location:** `S3Service.cs:117`
- **Effort:** Small

**Evidence:**
```csharp
public string GetPresignedUrl(string key, int expiresInSeconds = 3600)
```

**Impact:** Callers can request arbitrarily long presigned URLs by passing a large `expiresInSeconds` value, potentially exposing private assets indefinitely.

**Recommendation:** Enforce a maximum TTL (e.g., 86400 seconds) and reject or clamp values exceeding it.

---

### Low

#### Deprecated X-XSS-Protection Header

- **Category:** Infrastructure
- **Location:** `SecurityHeadersMiddleware.cs:16`
- **Effort:** Small

**Evidence:**
```csharp
context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
```

**Impact:** This header is deprecated and has been shown to introduce XSS vulnerabilities in some older browsers. Modern best practice is to rely on CSP.

**Recommendation:** Remove the `X-XSS-Protection` header.

---

#### Missing HTTPS/HSTS Middleware

- **Category:** Infrastructure
- **Location:** `Program.cs`
- **Effort:** Small

**Impact:** `Program.cs` does not call `UseHsts()` or `UseHttpsRedirection()`. If the reverse proxy is misconfigured, production traffic may be served over HTTP.

**Recommendation:** Add `app.UseHsts()` and `app.UseHttpsRedirection()` before `UseSecurityHeaders()` (gated by `!IsDevelopment()` if desired).

---

#### Audit Logging Write Failures Silently Swallowed Without Metrics

- **Category:** ABAC
- **Location:** `AbacService.cs:255-258`, `AbacService.cs:315-318`
- **Effort:** Small

**Evidence:**
```csharp
catch
{
    // Audit failures must never block permission checks
}
```

**Impact:** While the fail-open approach for audit logging is correct, completely silent swallowing means administrators have no visibility into audit system failures (e.g., database outages).

**Recommendation:** Inject `ILogger<AbacService>` and log audit/cache write failures at `Warning` level.

---

#### Asset Delete Catches All Exceptions Silently

- **Category:** Data Protection
- **Location:** `AssetMutations.cs:82-88`
- **Effort:** Small

**Evidence:**
```csharp
try
{
    await s3Service.DeleteAsync(asset.Path);
}
catch
{
}
```

**Impact:** S3 deletion failures are silently ignored. The database record is removed but the underlying file may remain in storage, causing orphaned files and potential data retention policy violations.

**Recommendation:** Log the exception with `ILogger` and consider returning a warning to the caller or scheduling a background cleanup job.

---

## Recommendations (Prioritized)

1. **Fix ABAC policy names on RoleQueries and PolicyQueries** (Small) — Change to `Roles:Read` and `Policies:Read` to prevent unauthorized enumeration.
2. **Enable JWT audience validation** (Small) — Set `ValidateAudience = true` and define a valid audience in both `Program.cs` and `AuthService.cs`.
3. **Remove hardcoded admin password fallback** (Small) — Throw if `Admin:Password` is not configured instead of defaulting to `"admin123"`.
4. **Add missing security headers** (Small) — Include HSTS and CSP in `SecurityHeadersMiddleware.cs`.
5. **Enforce rate limiting on asset downloads** (Small) — Apply `.RequireRateLimiting()` to the `/assets/{id}` endpoint.
6. **Bound S3 presigned URL TTL** (Small) — Clamp `expiresInSeconds` to a reasonable maximum.
7. **Remove development JWT TTL override from code** (Small) — Move local dev overrides to configuration files.
8. **Align refresh token TTL with policy** (Small) — Change default from 30 days to 7 days.
9. **Add CORS policy** (Small) — Register an explicit allowlist-based CORS policy.
10. **Use HTTP-native errors in REST endpoints** (Small) — Replace `GraphQLException` with `Results.*` helpers in `AssetEndpoints.cs`.
11. **Add logging to swallowed exceptions** (Small) — Log ABAC audit/cache and S3 delete failures instead of silently catching.
12. **Add HTTPS middleware** (Small) — Include `UseHsts` and `UseHttpsRedirection` in production pipeline.

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
- `TechtonicCmsApi/Security/AbacAuthorizationHandler.cs`
- `TechtonicCmsApi/Security/ApiKeyAuthenticationHandler.cs`
- `TechtonicCmsApi/Security/SecurityHeadersMiddleware.cs`
- `TechtonicCmsApi/Types/Assets/AssetEndpoints.cs`
- `TechtonicCmsApi/Types/Assets/AssetMutations.cs`
- `TechtonicCmsApi/Types/Assets/AssetQueries.cs`
- `TechtonicCmsApi/Types/Auth/AuthMutations.cs`
- `TechtonicCmsApi/Types/Auth/AuthQueries.cs`
- `TechtonicCmsApi/Types/Users/UserMutations.cs`
- `TechtonicCmsApi/Types/Users/UserQueries.cs`
- `TechtonicCmsApi/Types/Roles/RoleQueries.cs`
- `TechtonicCmsApi/Types/Roles/RoleMutations.cs`
- `TechtonicCmsApi/Types/Policies/PolicyQueries.cs`
- `TechtonicCmsApi/Types/Policies/PolicyMutations.cs`
- `TechtonicCmsApi/Types/ApiKeys/ApiKeyMutations.cs`
- `TechtonicCmsApi/Types/ApiKeys/ApiKeyQueries.cs`
- `TechtonicCmsApi/Types/Collections/CollectionQueries.cs`
- `TechtonicCmsApi/Types/Collections/CollectionMutations.cs`
- `TechtonicCmsApi/Types/Collections/DynamicCollections/CollectionTypeModuleQueries.cs`
- `TechtonicCmsApi/Types/Collections/DynamicCollections/CollectionTypeModuleMutations.cs`
- `TechtonicCmsApi/Types/Collections/DynamicCollections/DynamicCollectionHelpers.cs`
- `TechtonicCmsApi/Types/Audit/AuditQueries.cs`
- `TechtonicCmsApi/Types/Entries/EntryTypes.cs`
- `TechtonicCmsApi/Schema/TechtonicCms/Enums/AttributePath.cs`
- `TechtonicCmsApi/Schema/TechtonicCms/Enums/UserStatus.cs`
- `.dev.env`
- `compose.dev.yaml`

### Policy Coverage Matrix

The `AdminBootstrapService` iterates over all `BaseResource` × `PermissionAction` combinations and seeds Allow policies with priority 1000 for the admin role. This covers every enum combination, including `Draft`, `Transform`, and `ConfigureFields` which have no corresponding mutations. The seeded policies provide full admin coverage.

Dynamic collection entry mutations (`create`, `update`, `delete`, `publish`, `unpublish`, `archive`, `restore`) and queries all invoke `abacService.RequirePermissionAsync` with appropriate `resourceData`, ensuring ABAC mediation is complete for the entry resource type.
