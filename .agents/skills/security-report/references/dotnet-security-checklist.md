# .NET / ASP.NET Core Security Checklist

## JWT Authentication

- [ ] **Algorithm**: Use `SecurityAlgorithms.RsaSha256` (RS256). Reject `none`, `HS256` when using asymmetric keys.
- [ ] **Key handling**: Private key must not be in source control. Load from env/vault.
- [ ] **Validation**: `ValidateIssuer`, `ValidateAudience`, `ValidateLifetime`, `ValidateIssuerSigningKey` all enabled.
- [ ] **Clock skew**: `ClockSkew` should be minimal (≤ 5 min) or zero.
- [ ] **Token lifetime**: Access tokens ≤ 15 min; refresh tokens ≤ 7 days with rotation.
- [ ] **Binding**: Consider `x5t` or `kid` validation to prevent key confusion.

## Password Storage

- [ ] **Algorithm**: BCrypt (or Argon2id/Scrypt). Minimum work factor 12.
- [ ] **Legacy migration**: Transparent upgrade path from SHA256/MD5/PBKDF2 to BCrypt.
- [ ] **Salting**: Never custom salt; use algorithm-provided salt.

## Session Management

- [ ] **Storage**: Server-side (Redis) with random session ID.
- [ ] **TTL**: Short (15 min) with sliding expiration or explicit refresh.
- [ ] **Revocation**: Immediate invalidation on logout/password change.
- [ ] **Transport**: `HttpOnly`, `Secure`, `SameSite=Strict` cookies if cookie-based.

## EF Core / SQL Injection

- [ ] **Parameterized queries**: Always use LINQ or `FromSqlInterpolated`.
- [ ] **Raw SQL**: `FromSqlRaw` only with explicitly validated parameters.
- [ ] **Migrations**: No sensitive data in migration scripts.

## Middleware Pipeline

- [ ] **Order**: Authentication before Authorization before endpoints.
- [ ] **HTTPS**: `UseHsts` + `UseHttpsRedirection` in production.
- [ ] **CORS**: Explicit allowlist; never `AllowAnyOrigin` with credentials.
- [ ] **Anti-forgery**: Enabled for state-changing endpoints (less relevant for pure APIs, but verify).

## Security Headers

- [ ] `Strict-Transport-Security` (HSTS)
- [ ] `X-Content-Type-Options: nosniff`
- [ ] `X-Frame-Options: DENY`
- [ ] `Content-Security-Policy` (CSP)
- [ ] `Referrer-Policy: strict-origin-when-cross-origin`
