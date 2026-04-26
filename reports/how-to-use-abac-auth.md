# Techtonic CMS Auth & Permissions — Beginner Q&A

This document answers the most common questions new users ask about authentication and authorization in Techtonic CMS. It is designed to guide both users and GUI developers.

---

## Authentication (Logging In)

### Q: How do I log in for the first time?

When the server starts, it creates a default admin user. Check your environment configuration for `Admin:Name` and `Admin:Password` (or `.dev.env`). The defaults are usually:

- **Username:** `admin`
- **Password:** `admin123`

Send this GraphQL mutation:

```graphql
mutation {
  auth {
    login(name: "admin", password: "admin123") {
      accessToken { tokenValue expiresAt }
      refreshToken { tokenValue expiresAt }
      user { id name status }
    }
  }
}
```

Save the `accessToken.tokenValue`. From now on, include it in every request header:

```
Authorization: Bearer <your-access-token>
```

> **Important:** Change the default admin password immediately after first login.

---

### Q: My access token expired. Do I have to log in again?

No. Use your refresh token to get a new pair without re-entering your password:

```graphql
mutation {
  auth {
    refresh(refreshToken: "<your-refresh-token>") {
      accessToken { tokenValue }
      refreshToken { tokenValue }
    }
  }
}
```

Each refresh token can only be used once. The old one is invalidated and a new one is issued.

---

### Q: How do I log out?

```graphql
mutation {
  auth {
    logout { message }
  }
}
```

This immediately invalidates your session. Your access token becomes useless, even if it has not expired yet.

To log out everywhere (all devices):

```graphql
mutation {
  auth {
    logoutAll { message }
  }
}
```

---

### Q: What is an API key and when should I use it?

An API key is a long secret string you send in the `X-Api-Key` header instead of a JWT token. Use it for:

- Server-to-server scripts
- CI/CD pipelines
- Integrations that do not handle token refresh well

API keys can have expiration dates and work with the same permission system as regular users.

---

## Authorization Basics

### Q: Why was my request denied with "FORBIDDEN"?

The system uses **ABAC** (Attribute-Based Access Control). Every request is checked against **policies** that say "allow" or "deny." If no policy allows your action, you get `FORBIDDEN`.

The default behavior is **deny everything unless explicitly allowed**.

---

### Q: What are the three things I need to manage permissions?

1. **Users** — people who log in
2. **Roles** — named groups like "admin" or "editor"
3. **Policies** — the actual rules that say what is allowed

Think of it like a company:
- **User** = an employee
- **Role** = their job title
- **Policy** = the list of rooms their badge opens

You assign policies to roles, then assign roles to users. You can also assign policies directly to users for one-off exceptions.

---

### Q: What does the admin get by default?

On first startup, the system automatically creates:

- An **admin user**
- An **admin role**
- **64 policies** — one for every possible action on every resource, all set to "Allow"

The admin can do everything out of the box.

---

### Q: Can a user do anything without being given permissions?

Not much. Besides logging in, the default is deny. However, the system automatically grants **creator permissions**:

- If you create a collection, you can edit, delete, and manage its schema
- If you create an API key, you can manage it

These work by comparing the resource creator to the current user. No manual setup needed.

---

## Creating Roles and Policies

### Q: How do I create a new role?

```graphql
mutation {
  roles {
    create(input: {
      name: "editor"
      description: "Can manage content"
    }) {
      id
      name
    }
  }
}
```

Save the returned `id`. You will need it to assign the role to users.

---

### Q: How do I assign a role to a user?

```graphql
mutation {
  users {
    assignRole(input: {
      userId: "<USER_ID>"
      roleId: "<ROLE_ID>"
    })
  }
}
```

You can also set an expiration date:

```graphql
mutation {
  users {
    assignRole(input: {
      userId: "<USER_ID>"
      roleId: "<ROLE_ID>"
      expiresAt: "2026-05-26T23:59:59Z"
    })
  }
}
```

After that date, the role is automatically ignored.

---

### Q: How do I create a simple "allow everything" policy?

Create a policy with **no rules**. A policy with no rules always matches:

```graphql
mutation {
  policy {
    create(input: {
      name: "allow-all-collections-read"
      description: "Allow reading all collections"
      effect: Allow
      resourceType: Collections
      actionType: Read
      priority: 100
      isActive: true
      ruleConnector: And
      rules: []
    }) {
      id
    }
  }
}
```

Assign this policy to a role, then assign that role to users who need collection read access.

---

### Q: How do I write a rule? What is the `value` field?

Each rule compares an **attribute** to a **value** using an **operator**.

The `value` field is a typed input. You pick exactly one field based on what type of value you need:

| Value field | GraphQL type | Example | Use for |
|-------------|-------------|---------|---------|
| `stringValue` | `String` | `value: { stringValue: "editor" }` | Text comparisons |
| `numberValue` | `Float` | `value: { numberValue: 42 }` | Numbers |
| `booleanValue` | `Boolean` | `value: { booleanValue: true }` | True/false |
| `uuidValue` | `UUID` | `value: { uuidValue: "a1b2..." }` | IDs and GUIDs |
| `dateTimeValue` | `DateTime` | `value: { dateTimeValue: "2026-04-26T09:00:00Z" }` | Timestamps |
| `arrayValue` | `[String!]` | `value: { arrayValue: ["editor", "admin"] }` | Lists (with `In` / `NotIn`) |
| `contextReferencePath` | `AttributePath` | `value: { contextReferencePath: SubjectId }` | `EqContextRef` only |

**You must provide exactly one field.** The system figures out the `valueType` automatically.

**Special case:** `IsNull` and `IsNotNull` operators do **not** need a `value` at all.

---

### Q: How do I create a policy that only allows certain roles?

Use the `SubjectRole` attribute with the `In` operator and an `arrayValue`:

```graphql
mutation {
  policy {
    create(input: {
      name: "editor-can-read-entries"
      effect: Allow
      resourceType: Entries
      actionType: Read
      priority: 500
      isActive: true
      ruleConnector: And
      rules: [
        {
          attributePath: SubjectRole
          operator: In
          value: { arrayValue: ["editor", "admin"] }
          isActive: true
          order: 1
        }
      ]
    }) {
      id
    }
  }
}
```

Only users with the "editor" or "admin" role will match this policy.

---

### Q: How do I let users only edit their own content?

Use the `EqContextRef` operator with `contextReferencePath`:

```graphql
mutation {
  policy {
    create(input: {
      name: "update-own-entries"
      effect: Allow
      resourceType: Entries
      actionType: Update
      priority: 500
      isActive: true
      ruleConnector: And
      rules: [
        {
          attributePath: ResourceEntryCreatedBy
          operator: EqContextRef
          value: { contextReferencePath: SubjectId }
          isActive: true
          order: 1
        }
      ]
    }) {
      id
    }
  }
}
```

This means: "Allow if the person who created the entry is the same as the current user." No hard-coded user IDs needed.

---

### Q: How do I restrict someone to only one specific collection?

Use `ResourceEntryCollectionId` with the `Eq` operator and a `uuidValue`:

```graphql
mutation {
  policy {
    create(input: {
      name: "blog-only-editor"
      effect: Allow
      resourceType: Entries
      actionType: Update
      priority: 500
      isActive: true
      ruleConnector: And
      rules: [
        {
          attributePath: ResourceEntryCollectionId
          operator: Eq
          value: { uuidValue: "e5f6g7h8-1111-2222-3333-444444444444" }
          isActive: true
          order: 1
        }
      ]
    }) {
      id
    }
  }
}
```

For multiple collections, use `In` with `arrayValue`:

```graphql
{
  attributePath: ResourceEntryCollectionId
  operator: In
  value: { arrayValue: ["uuid-1", "uuid-2"] }
}
```

---

### Q: How do I block access during certain hours?

Create a **Deny** policy with high priority. Deny policies are checked first:

```graphql
mutation {
  policy {
    create(input: {
      name: "no-access-after-hours"
      effect: Deny
      resourceType: Entries
      actionType: Read
      priority: 2000
      isActive: true
      ruleConnector: Or
      rules: [
        {
          attributePath: EnvironmentCurrentTime
          operator: Lt
          value: { dateTimeValue: "2026-04-26T09:00:00Z" }
          order: 1
        },
        {
          attributePath: EnvironmentCurrentTime
          operator: Gt
          value: { dateTimeValue: "2026-04-26T17:00:00Z" }
          order: 2
        }
      ]
    }) {
      id
    }
  }
}
```

`priority: 2000` ensures this runs before normal Allow policies (which usually have priority 100-1000).

---

## Common Permission Scenarios

### Q: How do I make an "editor" who can create and edit blog posts?

**Step 1:** Create policies for Create, Update, and Read on Entries:

```graphql
mutation {
  p1: policy {
    create(input: {
      name: "editor-create-entries"
      effect: Allow
      resourceType: Entries
      actionType: Create
      priority: 500
      ruleConnector: And
      rules: [{ attributePath: SubjectRole operator: In value: { arrayValue: ["editor"] } order: 1 }]
    }) { id }
  }
  p2: policy {
    create(input: {
      name: "editor-read-entries"
      effect: Allow
      resourceType: Entries
      actionType: Read
      priority: 500
      ruleConnector: And
      rules: [{ attributePath: SubjectRole operator: In value: { arrayValue: ["editor"] } order: 1 }]
    }) { id }
  }
  p3: policy {
    create(input: {
      name: "editor-update-own-entries"
      effect: Allow
      resourceType: Entries
      actionType: Update
      priority: 500
      ruleConnector: And
      rules: [
        { attributePath: SubjectRole operator: In value: { arrayValue: ["editor"] } order: 1 }
        { attributePath: ResourceEntryCreatedBy operator: EqContextRef value: { contextReferencePath: SubjectId } order: 2 }
      ]
    }) { id }
  }
}
```

**Step 2:** Create the "editor" role and assign the policies.

**Step 3:** Assign the role to users.

---

### Q: How do I make a "viewer" who can only read but not change anything?

Create a single policy for each resource they should view:

```graphql
mutation {
  policy {
    create(input: {
      name: "viewer-read-collections"
      effect: Allow
      resourceType: Collections
      actionType: Read
      priority: 100
      ruleConnector: And
      rules: [{ attributePath: SubjectRole operator: In value: { arrayValue: ["viewer"] } }]
    }) { id }
  }
}
```

Create similar policies for `Entries:Read`, `Assets:Read`, etc. Assign them to a "viewer" role.

> **Important:** Do not give them Create, Update, or Delete policies. The default is deny, so they will not be able to modify anything.

---

### Q: How do I give someone temporary access?

When assigning a role or policy, include an `expiresAt`:

```graphql
mutation {
  users {
    assignRole(input: {
      userId: "<USER_ID>"
      roleId: "<ROLE_ID>"
      expiresAt: "2026-05-01T00:00:00Z"
    })
  }
}
```

After the expiration date, the assignment is ignored. The user keeps their account but loses those permissions.

---

### Q: How do I deactivate a user without deleting them?

```graphql
mutation {
  users {
    deactivate(id: "<USER_ID>") {
      id
      status
    }
  }
}
```

This:
- Sets their status to `Inactive`
- Logs them out everywhere immediately
- Prevents them from logging in or refreshing tokens

Their data and role assignments are preserved. To reactivate:

```graphql
mutation {
  users {
    activate(id: "<USER_ID>") {
      id
      status
    }
  }
}
```

---

## Troubleshooting

### Q: I created a policy but it is not working. Why?

Common reasons:

1. **Not assigned.** Policies do nothing until assigned to a role or user. Check `policy.assignedToRoles` and `policy.assignedToUsers`.
2. **Inactive.** Make sure `isActive: true` on both the policy and the assignment.
3. **Expired assignment.** Check if `expiresAt` has passed on the `UserRole` or `UserPolicy` record.
4. **Wrong resource/action.** A policy for `Collections:Read` will not affect `Entries:Update`.
5. **Cached result.** Permission decisions are cached for a few minutes. Wait a moment or check the audit log.
6. **Priority issue.** If a Deny policy with higher priority matches first, your Allow policy never gets checked.
7. **Wrong value field.** Make sure you use the correct value type for your operator:
   - `In`/`NotIn` require `arrayValue`
   - `EqContextRef` requires `contextReferencePath`
   - `IsNull`/`IsNotNull` must **not** have a `value`
   - Everything else uses `stringValue`, `numberValue`, `booleanValue`, `uuidValue`, or `dateTimeValue`

---

### Q: How do I debug why someone was denied?

Query the audit log:

```graphql
query {
  abacAudits {
    nodes {
      userId
      resourceType
      requestedAction
      decision
      decisionReason
      timestamp
    }
  }
}
```

Look at `decisionReason`:
- `"Denied by policy '...' (priority ...)`" — a Deny policy matched
- `"Allowed by policy '...' (priority ...)`" — an Allow policy matched
- `"No matching allow policy found"` — no policy matched, default deny
- `"No policies assigned to user"` — the user has no applicable policies at all

---

### Q: What does priority do?

Priority is a number. Higher numbers are evaluated first.

- Deny policies should usually have **higher** priority than Allow policies.
- The default admin policies have priority `1000`.
- Creator policies have priority `500`.

Example flow for a request:

1. Check Deny policies at priority 2000 — none match
2. Check Deny policies at priority 1500 — none match
3. Check Allow policies at priority 1000 — admin policy matches — **ALLOWED**

If a Deny policy at priority 2000 had matched, the flow would stop at step 1 with **DENIED**.

---

### Q: What is the difference between `And` and `Or` rule connectors?

- **And** — ALL rules must match for the policy to match
- **Or** — ANY rule matching is enough

Example with And:
```
Rule 1: SubjectRole In ["editor"]
Rule 2: ResourceEntryCreatedBy EqContextRef SubjectId
```
User must be an editor AND the entry creator.

Example with Or:
```
Rule 1: SubjectRole In ["admin"]
Rule 2: ResourceEntryCreatedBy EqContextRef SubjectId
```
User is allowed if they are an admin OR the entry creator.

---

## Quick Reference Tables

### Resources

| Resource | Protects |
|----------|----------|
| `Users` | User accounts |
| `Collections` | Content schemas |
| `Entries` | Content items |
| `Assets` | Uploaded files |
| `ApiKeys` | API keys |
| `Policies` | ABAC rules themselves |
| `Roles` | Role definitions |

### Actions

| Action | Use For |
|--------|---------|
| `Create` | Making new things |
| `Read` | Viewing things |
| `Update` | Editing things |
| `Delete` | Removing things |
| `Publish` | Making entries live |
| `Unpublish` | Hiding entries |
| `Schedule` | Future publishing |
| `Archive` | Soft-deleting entries |
| `Restore` | Recovering entries |
| `Activate` | Enabling users |
| `Deactivate` | Disabling users |
| `Upload` | File uploads |
| `Download` | File downloads |
| `ManageSchema` | Changing collection fields |

### Operators and Their Required Value Types

| Operator | Required value field | Example |
|----------|---------------------|---------|
| `Eq` | `stringValue`, `numberValue`, `booleanValue`, `uuidValue`, or `dateTimeValue` | `value: { stringValue: "editor" }` |
| `Ne` | same as `Eq` | `value: { uuidValue: "a1b2..." }` |
| `In` | `arrayValue` (required) | `value: { arrayValue: ["editor", "admin"] }` |
| `NotIn` | `arrayValue` (required) | `value: { arrayValue: ["banned"] }` |
| `Gt` / `Gte` | `numberValue` or `dateTimeValue` | `value: { numberValue: 18 }` |
| `Lt` / `Lte` | `numberValue` or `dateTimeValue` | `value: { dateTimeValue: "2026-04-26T17:00:00Z" }` |
| `Contains` | `stringValue` | `value: { stringValue: "admin" }` |
| `StartsWith` / `EndsWith` | `stringValue` | `value: { stringValue: "prod-" }` |
| `Regex` | `stringValue` | `value: { stringValue: "^admin.*" }` |
| `IsNull` | **none** (omit `value`) | — |
| `IsNotNull` | **none** (omit `value`) | — |
| `EqContextRef` | `contextReferencePath` (required) | `value: { contextReferencePath: SubjectId }` |

### Rule Value Input Fields

| Field | GraphQL Type | Used With Operators |
|-------|-------------|---------------------|
| `stringValue` | `String` | `Eq`, `Ne`, `Contains`, `StartsWith`, `EndsWith`, `Regex` |
| `numberValue` | `Float` | `Eq`, `Ne`, `Gt`, `Gte`, `Lt`, `Lte` |
| `booleanValue` | `Boolean` | `Eq`, `Ne` |
| `uuidValue` | `UUID` | `Eq`, `Ne` |
| `dateTimeValue` | `DateTime` | `Eq`, `Ne`, `Gt`, `Gte`, `Lt`, `Lte` |
| `arrayValue` | `[String!]` | `In`, `NotIn` |
| `contextReferencePath` | `AttributePath` | `EqContextRef` |

---

## GUI Design Notes

Based on these questions, a user-friendly GUI should expose:

### Users Page
- [ ] List users with status (Active/Inactive)
- [ ] Create user with optional initial roles/policies
- [ ] Assign/unassign roles with optional expiration picker
- [ ] Assign/unassign policies with optional expiration and reason fields
- [ ] Activate/deactivate buttons
- [ ] Change password (self vs admin)
- [ ] "Impersonate" or "View effective permissions" button

### Roles Page
- [ ] List roles with assigned policy count
- [ ] Create role with optional initial policies
- [ ] Assign/unassign policies
- [ ] Show which users have this role

### Policies Page
- [ ] List policies with effect (Allow/Deny), resource, action, priority
- [ ] Visual rule builder:
  - Attribute dropdown
  - Operator dropdown
  - Dynamic value input that changes based on operator:
    - Text input for `stringValue`
    - Number input for `numberValue`
    - Toggle for `booleanValue`
    - UUID picker for `uuidValue`
    - Date/time picker for `dateTimeValue`
    - Tag/multi-select for `arrayValue`
    - Attribute dropdown for `contextReferencePath` (when operator is `EqContextRef`)
    - No value input for `IsNull`/`IsNotNull`
- [ ] Toggle active/inactive
- [ ] Show assigned roles and users
- [ ] Copy/duplicate policy button
- [ ] Priority slider or number input with guidance ("Deny policies: 1000+, Allow policies: 100-500")

### Audit Page
- [ ] Filter by user, resource, action, decision
- [ ] Show decision reason prominently
- [ ] Timeline view of recent checks

### Permission Preview
- [ ] Before saving a policy, show a preview: "This policy would affect X users"
- [ ] Test mode: simulate a permission check for a specific user + resource + action
