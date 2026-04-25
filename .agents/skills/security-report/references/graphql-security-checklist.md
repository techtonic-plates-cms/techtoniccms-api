# GraphQL / Hot Chocolate Security Checklist

## OWASP GraphQL Top 10 Mapping

- [ ] **GQL1: Injection** — All inputs validated/sanitized; no raw SQL in resolvers.
- [ ] **GQL2: Broken Authentication** — Every query/mutation has `[Authorize]` or `[AllowAnonymous]` explicitly.
- [ ] **GQL3: Broken Access Control** — ABAC checks on every resolver returning sensitive data.
- [ ] **GQL4: Improper Asset Management** — Introspection disabled in production unless required.
- [ ] **GQL5: Injection (Batching)** — Query batching limited or authenticated.
- [ ] **GQL6: Information Disclosure** — Exception details hidden in production.
- [ ] **GQL7: Denial of Service** — Query depth and cost limits configured.
- [ ] **GQL8: Mass Assignment** — Input types whitelist only mutable fields.
- [ ] **GQL9: Security Misconfiguration** — CORS, headers, and TLS properly set.
- [ ] **GQL10: Unsafe File Uploads** — MIME validation, size limits, path traversal checks.

## Hot Chocolate Specifics

- [ ] **Depth limiting**: `AddMaxExecutionDepthRule(n)` or `SetMaxExecutionDepth`
- [ ] **Cost analysis**: `AddMaxFieldCost`, `AddMaxTypeCost` configured with sensible defaults
- [ ] **Introspection**: Disable in production (`ModifyRequestOptions(o => o.IncludeExceptionDetails = false)`)
- [ ] **Error filtering**: `AddErrorFilter` to strip stack traces and internal details
- [ ] **Authorization**: `[Authorize]` applied to ObjectTypes, not just fields, to prevent field bypass
- [ ] **Paging limits**: `MaxPageSize` enforced on all list resolvers

## File Uploads (if applicable)

- [ ] **Size limits**: Enforced before stream processing
- [ ] **Extension whitelist**: Reject executable extensions
- [ ] **MIME verification**: Magic bytes check, not just `Content-Type`
- [ ] **Path sanitization**: No user-controlled filenames in storage paths
- [ ] **Storage isolation**: Uploaded files not served from same origin as app
