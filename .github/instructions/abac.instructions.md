---
description: "Use when modifying ABAC authorization logic, security policies, access control rules, or permission evaluation. Covers the deny-first evaluation model, policy CRUD, context attributes, and the authorization handler pipeline."
applyTo: "**/Security/**,**/Services/AbacService.cs"
---

# ABAC Authorization Guidelines

## Deny-First Evaluation Model

The `AbacService` evaluates policies in strict order:

1. Collect user's active roles (respecting `ExpiresAt` TTL on `UserRole`)
2. Gather role-assigned + directly-assigned policies (also respecting `ExpiresAt`)
3. Filter by `ResourceType`, `ActionType`, `IsActive`
4. **Deny-first**: Evaluate all `Deny` policies sorted by `Priority`. Any match = immediate denial.
5. **Allow by priority**: Evaluate `Allow` policies in priority order. First match = approval.
6. No matching Allow = denied (implicit deny)

**Never** change this evaluation order without updating both `AbacService` and the security tests.

## Policy Resources & Actions

5 resources × 19 actions = 95 admin policies (seeded by `AdminBootstrapService`):

| Resource (`BaseResource` enum) | Actions (`PermissionAction` enum) |
|------|------|
| `Users` | Create, Read, Update, Delete, Ban, Unban, Activate, Deactivate |
| `Collections` | Create, Read, Update, Delete, ManageSchema |
| `Entries` | Create, Read, Update, Delete, Publish, Unpublish, Schedule, Archive, Restore, Draft |
| `Assets` | Upload, Download, Read, Update, Delete, Transform |
| `Fields` | Read, Update, ConfigureFields |

When adding a new resource: update `BaseResource` enum, add to `SecurityPolicies.Register()`, update `AdminBootstrapService.SeedAsync()`, and add `PermissionAction` values if needed.

## Policy Rule System

Each `AbacPolicy` has ordered `AbacPolicyRule`s connected by `RuleConnector` (And/Or):

- **30 context attributes** (`AttributePath` enum): `SubjectId`, `SubjectRole`, `SubjectStatus`, `ResourceCollectionId`, `ResourceEntryStatus`, `ResourceFieldIsPii`, `EnvironmentCurrentTime`, etc.
- **14 operators** (`OperatorType`): `Eq`, `Ne`, `In`, `NotIn`, `Gt`, `Gte`, `Lt`, `Lte`, `Contains`, `StartsWith`, `EndsWith`, `IsNull`, `IsNotNull`, `Regex`
- **6 value types** (`ValueType`): `String`, `Number`, `Boolean`, `Uuid`, `Datetime`, `Array`

Rules with empty/null `ExpectedValue` and `IsNull`/`IsNotNull` operators check attribute existence. Type coercion happens at evaluation time (e.g., string → double for `Gt`/`Lt`).

## Authorization Handler Pipeline

`AbacAuthorizationHandler` implements `IAuthorizationHandler`:

1. Extracts `userId` from JWT claims (`"userId"`, `ClaimTypes.NameIdentifier`, `"sub"` — in that order)
2. Creates `AbacRequirement(BaseResource, PermissionAction)` from the policy
3. Calls `abacService.CheckPermissionAsync(userId, resource, action)`

**Policy registration** in `SecurityPolicies.Register()` creates a policy per resource×action with name format `"{Resource}:{Action}"` (e.g., `"Entries:Publish"`).

## Adding New Permission Checks

In GraphQL resolvers, use two layers:

```csharp
[Authorize(Policy = "Entries:Publish")]  // Gate: ASP.NET middleware
public async Task<Entry> Publish(Guid id, ...)
{
    await abacService.RequirePermissionAsync(userId, BaseResource.Entries, PermissionAction.Publish);
    // ... business logic
}
```

The attribute provides a fast gate; the runtime check provides fine-grained ABAC with resource-specific context data.

## Audit & Caching

- All evaluations are logged to `AbacAudit` (decision, evaluated policies, evaluation time, IP, user agent).
- `AbacEvaluationCache` stores results with `ContextChecksum` and `PolicyVersions` for cache invalidation.
- Do NOT disable audit logging — it's required for compliance.
