# ABAC Engine Audit Guide

## Design Principles

- [ ] **Deny-first evaluation**: Deny policies evaluated before Allow policies.
- [ ] **Priority sorting**: Higher priority evaluated first within each effect (Deny/Allow).
- [ ] **Default deny**: If no Allow policy matches, result is `false`.
- [ ] **Complete mediation**: Every resource access goes through the ABAC engine.

## Cache Security

- [ ] **TTL separation**: Deny cache TTL should be ≤ Allow cache TTL (deny-first propagation).
- [ ] **Cache poisoning**: Cache key includes all context dimensions (`userId`, `resourceType`, `resourceId`, `action`, `fieldId`).
- [ ] **Invalidation**: `PolicyVersions` checksum detects stale cache entries after policy updates.
- [ ] **Sensitive data**: Cache entries must not contain PII or secrets.

## Policy Rules

- [ ] **EqContextRef validation**: The referenced context key must exist; missing key should fail closed (deny).
- [ ] **Regex safety**: `Regex` operator must have a timeout to prevent ReDoS.
- [ ] **Type safety**: `ValueType` must match the actual context value type; mismatches should fail closed.
- [ ] **Rule connector logic**: `And` = all rules must match; `Or` = any rule matches. Verify correct grouping.

## Context Attributes

- [ ] **Subject attributes**: `SubjectId`, `SubjectRole`, `SubjectStatus` populated from authenticated principal.
- [ ] **Resource attributes**: `ResourceEntryCreatedBy`, `ResourceAssetUploadedBy`, `ResourceEntryStatus` passed by resolvers.
- [ ] **Environment attributes**: `EnvironmentCurrentTime`, `EnvironmentIpAddress` populated at evaluation time.
- [ ] **Action attribute**: `ActionType` matches the requested action exactly.

## Audit Logging

- [ ] **Completeness**: Every evaluation (allow, deny, default deny) logged.
- [ ] **Integrity**: Audit records immutable; append-only.
- [ ] **Failure handling**: Audit write failures must not affect the access decision (fail open for logging, not for access).
- [ ] **Retention**: Defined retention policy for `AbacAudit` table.

## Bootstrap Validation

- [ ] **Policy coverage**: Every active `BaseResource` × `PermissionAction` combination has at least one Allow policy for admin.
- [ ] **No orphaned actions**: Enum values like `Draft`, `Transform`, `ConfigureFields` that have no mutations should not be exposed or should be explicitly excluded.
