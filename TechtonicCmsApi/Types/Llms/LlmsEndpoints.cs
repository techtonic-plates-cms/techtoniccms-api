using HotChocolate;
using HotChocolate.Execution;
using HotChocolate.Types;
using Microsoft.EntityFrameworkCore;
using TechtonicCmsApi.Contexts;
using TechtonicCmsApi.Schema.TechtonicCms.Entities;
using TechtonicCmsApi.Schema.TechtonicCms.Enums;
using TechtonicCmsApi.Types.Collections.DynamicCollections;

namespace TechtonicCmsApi.Types.Llms;

public static class LlmsEndpoints
{
    public static WebApplication MapLlmsEndpoints(this WebApplication app)
    {
        var enabled = app.Configuration.GetValue<bool>("Llms:EndpointEnabled");
        if (!enabled)
            return app;

        app.MapGet("/llms.md", async (
            IDbContextFactory<TechtonicCmsDbContext> dbFactory,
            IRequestExecutorResolver executorResolver,
            CancellationToken cancellationToken) =>
        {
            var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            await using var _ = db.ConfigureAwait(false);

            var executor = await executorResolver.GetRequestExecutorAsync(cancellationToken: cancellationToken);
            var schema = executor.Schema;

            var collections = await db.Collections
                .AsNoTracking()
                .Include(c => c.Fields)
                .ThenInclude(f => f.RelatedCollection)
                .OrderBy(c => c.Name)
                .ToListAsync(cancellationToken);

            var markdown = BuildMarkdown(collections, schema);
            return Results.Text(markdown, "text/markdown; charset=utf-8");
        })
        .RequireRateLimiting("GeneralApi");

        return app;
    }

    private static string BuildMarkdown(IReadOnlyList<Collection> collections, ISchema schema)
    {
        var sections = new List<string>
        {
            "# Techtonic CMS — LLM Integration Guide",
            "",
            "> This document is auto-generated from the live system configuration. It describes how to authenticate, authorize, and interact with the Techtonic CMS GraphQL API and REST endpoints.",
            "",
            "## Table of Contents",
            "",
            "- [System Overview](#system-overview)",
            "- [Authentication](#authentication)",
            "- [Authorization (ABAC)](#authorization-abac)",
            "- [Static GraphQL Resources](#static-graphql-resources)",
            "- [REST Endpoints](#rest-endpoints)",
            "- [Dynamic Content Collections](#dynamic-content-collections)",
            "",
            BuildSystemOverview(),
            BuildAuthentication(),
            BuildAuthorization(),
            BuildStaticResources(),
            BuildRestEndpoints(),
            BuildDynamicCollections(collections, schema),
        };

        return string.Join("\n", sections);
    }

    private static string BuildSystemOverview()
    {
        return """

            ## System Overview

            Techtonic CMS is a headless content management system built on .NET 10 with Hot Chocolate GraphQL, PostgreSQL, Redis sessions, and S3-compatible asset storage.

            - **GraphQL endpoint:** `http://localhost:5095/graphql` (Banana Cake Pop available at `/graphql` in development)
            - **Authentication:** JWT Bearer (RS256) or API Key
            - **Authorization:** Custom ABAC engine (deny-first, priority-based policies)
            - **Entry data storage:** Scalar fields in PostgreSQL JSONB; relations in a dedicated join table
            - **Dynamic schema:** User-defined collections generate runtime GraphQL types. The schema refreshes automatically when collections or fields are mutated.

            """;
    }

    private static string BuildAuthentication()
    {
        return """

            ## Authentication

            All requests must include an `Authorization` header. Two schemes are supported.

            ### JWT Bearer

            1. Obtain tokens via the login mutation:
                ```graphql
                mutation {
                  auth {
                    login(email: "user@example.com", password: "...") {
                      accessToken
                      refreshToken
                    }
                  }
                }
                ```
            2. Use the access token in subsequent requests:
                ```
                Authorization: Bearer <accessToken>
                ```
            3. Refresh an expiring session:
                ```graphql
                mutation {
                  auth {
                    refresh(refreshToken: "...") {
                      accessToken
                      refreshToken
                    }
                  }
                }
                ```
            4. Revoke a session:
                ```graphql
                mutation {
                  auth {
                    logout(refreshToken: "...")
                  }
                }
                ```

            - Access tokens are stateless RS256 JWTs.
            - Sessions are stored in Redis with a 15-minute TTL and can be revoked.
            - Refresh tokens are stored in Redis with a 7-day TTL.

            ### API Key

            Some integrations may use API keys instead of JWTs:
            ```
            Authorization: ApiKey <apiKey>
            ```

            """;
    }

    private static string BuildAuthorization()
    {
        return """

            ## Authorization (ABAC)

            The CMS uses a custom Attribute-Based Access Control (ABAC) engine. Authorization is **deny-first**.

            ### Evaluation Flow

            1. **Cache lookup** — checks a short-lived evaluation cache.
            2. **Policy resolution** — loads active policies attached to the user directly or via roles.
            3. **Deny evaluation** — deny policies are sorted by descending priority. The first match results in immediate denial.
            4. **Allow evaluation** — allow policies are sorted by descending priority. The first match results in approval.
            5. **Default deny** — if no allow policy matches, access is denied.

            ### Common Context Attributes

            When evaluating policies, the engine builds a context dictionary that includes:

            - `SubjectId` — the caller's user UUID
            - `SubjectRole` — comma-separated role names
            - `SubjectStatus` — `Active` or `Inactive`
            - `EnvironmentCurrentTime` — UTC ISO 8601 timestamp
            - `EnvironmentIpAddress`
            - `EnvironmentUserAgent`
            - `ActionType` — the action being attempted (e.g., `CREATE`, `READ`, `UPDATE`, `DELETE`)
            - Resource attributes such as:
              - `ResourceEntryCreatedBy`
              - `ResourceEntryCollectionId`
              - `ResourceCollectionCreatedBy`
              - `ResourceAssetUploadedBy`
              - And more depending on the resource type

            ### Error Codes

            - `UNAUTHENTICATED` — missing or invalid credentials
            - `FORBIDDEN` — ABAC policy denied the request
            - `NOT_FOUND` — requested resource does not exist
            - `CONFLICT` — unique constraint violation or duplicate slug
            - `BAD_REQUEST` — validation failure
            - `INVALID_ENUM` — an enum value was not recognized

            """;
    }

    private static string BuildStaticResources()
    {
        return """

            ## Static GraphQL Resources

            In addition to the dynamic collections described below, the schema exposes the following static domains. You can introspect `/graphql` for exact field definitions.

            | Domain | Query Root | Mutation Root | Key Operations |
            |--------|-----------|---------------|----------------|
            | Users | `users` | `users` | CRUD, role assignment |
            | Roles | `roles` | `roles` | CRUD, policy assignment |
            | Policies | `policies` | `policies` | CRUD, rule management |
            | Collections | `collections` | `collections` | Define content schemas |
            | Assets | `assets` | `assets` | List, manage metadata |
            | API Keys | `apiKeys` | `apiKeys` | Create, revoke |
            | Audit | `audit` | — | Read ABAC audit logs |
            | Auth | `auth` | `auth` | Login, refresh, logout |
            | Entries | `entries` | `entries` | **Dynamic** — see next section |

            All list queries support `[UsePaging(MaxPageSize = 100)]`, `[UseFiltering]`, and `[UseSorting]`.

            """;
    }

    private static string BuildRestEndpoints()
    {
        return """

            ## REST Endpoints

            The CMS is primarily GraphQL, but two REST endpoints exist for asset handling:

            ### `POST /assets/upload`

            Upload a file. Returns the created `Asset` record.

            - **Content-Type:** `multipart/form-data`
            - **Fields:**
              - `file` (required) — the file to upload
              - `alt` (optional) — alt text
              - `caption` (optional) — caption
              - `isPublic` (optional) — boolean
            - **Rate limiter:** `Upload`
            - **Max size:** 50 MB
            - **Allowed extensions:** jpg, jpeg, png, gif, webp, svg, pdf, doc, docx, txt, csv, mp3, mp4, ppt, pptx, xls, xlsx, avi, mov, mkv, md

            ### `GET /assets/{id}`

            Download or stream an asset by its UUID.

            """;
    }

    private static string BuildDynamicCollections(IReadOnlyList<Collection> collections, ISchema schema)
    {
        var lines = new List<string>
        {
            "",
            "## Dynamic Content Collections",
            "",
            "User-defined collections generate runtime GraphQL types. Each collection becomes a set of query fields, mutations, filter inputs, and sort inputs. This section reflects the collections currently configured in the system.",
            "",
            "### Entry Status Lifecycle",
            "",
            "Entries follow a status workflow: **Draft → Published → Archived / Deleted**. Dedicated mutations exist for each transition.",
            "",
            "- `create` — creates a new entry (status defaults to `Draft`)",
            "- `update` — updates entry fields",
            "- `publish` — changes status to `Published`",
            "- `unpublish` — changes status to `Draft`",
            "- `archive` — changes status to `Archived`",
            "- `restore` — changes status to `Draft`",
            "- `delete` — changes status to `Deleted`",
            "",
            "### Scalar Type Mapping",
            "",
            "| Field Data Type | GraphQL Input Type | GraphQL Output Type | Storage |",
            "|-----------------|-------------------|---------------------|---------|",
            "| Text | `String` | `String` | JSONB |",
            "| Boolean | `Boolean` | `Boolean` | JSONB |",
            "| Number | `Float` | `Float` | JSONB |",
            "| DateTime | `DateTime` | `DateTime` | JSONB |",
            "| Relation | `String` (entry UUID) | Target entry type | `entry_relations` table |",
            "| Asset | `String` (asset UUID) | `String` | JSONB |",
            "| Object | `String` (raw JSON) | `String` | JSONB |",
            "",
            "### Localization",
            "",
            "When a collection is localized, entries have a `locale` and `defaultLocale`. Collections define `defaultLocale` and `supportedLocales`.",
            "",
        };

        if (collections.Count == 0)
        {
            lines.Add("### No Collections Defined");
            lines.Add("");
            lines.Add("There are currently no content collections in the system.");
            lines.Add("");
            return string.Join("\n", lines);
        }

        foreach (var collection in collections)
        {
            lines.AddRange(BuildCollectionSection(collection, schema));
        }

        return string.Join("\n", lines);
    }

    private static IEnumerable<string> BuildCollectionSection(Collection collection, ISchema schema)
    {
        var pascal = DynamicCollectionHelpers.ToPascalCase(collection.Slug);
        var camel = DynamicCollectionHelpers.ToCamelCase(collection.Slug);
        var entryTypeName = $"{pascal}Entry";
        var dataTypeName = $"{pascal}EntryData";
        var createInputName = $"{pascal}CreateEntryDataInput";
        var updateInputName = $"{pascal}UpdateEntryDataInput";
        var filterInputName = $"{pascal}EntryFilterInput";
        var sortInputName = $"{pascal}EntrySortInput";
        var mutationsTypeName = $"{pascal}Mutations";

        schema.TryGetType<ObjectType>(entryTypeName, out var entryType);
        schema.TryGetType<ObjectType>(dataTypeName, out var dataObjType);
        schema.TryGetType<InputObjectType>(createInputName, out var createInputType);
        schema.TryGetType<InputObjectType>(updateInputName, out var updateInputType);
        schema.TryGetType<ObjectType>(mutationsTypeName, out var mutType);

        var lines = new List<string>
        {
            $"### {collection.Name} (`{collection.Slug}`)",
            "",
        };

        if (!string.IsNullOrWhiteSpace(collection.Description))
        {
            lines.Add($"> {collection.Description}");
            lines.Add("");
        }

        lines.Add("**Metadata**");
        lines.Add("");
        lines.Add($"- **Default locale:** `{collection.DefaultLocale}`");
        lines.Add($"- **Supported locales:** {string.Join(", ", collection.SupportedLocales.Select(l => $"`{l}`"))}");
        lines.Add($"- **Localized:** {collection.IsLocalized}");
        if (!string.IsNullOrWhiteSpace(collection.Color))
            lines.Add($"- **Color:** `{collection.Color}`");
        lines.Add("");

        lines.Add("**Fields**");
        lines.Add("");

        if (collection.Fields.Count == 0)
        {
            lines.Add("*No fields defined for this collection.*");
            lines.Add("");
        }
        else
        {
            lines.Add("| Field | GraphQL Type | Required | Unique | Default | Description |");
            lines.Add("|-------|-------------|----------|--------|---------|-------------|");

            foreach (var field in collection.Fields.OrderBy(f => f.CreatedAt))
            {
                string gqlType;
                if (dataObjType is not null &&
                    dataObjType.Fields.FirstOrDefault(f => f.Name == field.Name) is { } schemaField)
                {
                    gqlType = FormatGraphQLType(schemaField.Type);
                }
                else
                {
                    gqlType = MapFieldDataType(field);
                    if (field.DataType == FieldDataType.Relation && field.RelatedCollection is not null)
                    {
                        var targetPascal = DynamicCollectionHelpers.ToPascalCase(field.RelatedCollection.Slug);
                        gqlType = $"{targetPascal}Entry";
                    }
                }

                var required = field.IsRequired ? "Yes" : "No";
                var unique = field.IsUnique ? "Yes" : "No";
                var defaultVal = string.IsNullOrWhiteSpace(field.DefaultValue) ? "—" : $"`{field.DefaultValue}`";
                var description = string.IsNullOrWhiteSpace(field.Description) ? "—" : field.Description;
                var label = string.IsNullOrWhiteSpace(field.Label) ? field.Name : field.Label;

                lines.Add($"| {label} (`{field.Name}`) | `{gqlType}` | {required} | {unique} | {defaultVal} | {description} |");
            }

            lines.Add("");
        }

        lines.Add("**Generated Types**");
        lines.Add("");

        if (entryType is not null)
        {
            var fieldList = string.Join(", ", entryType.Fields.Select(f => $"`{f.Name}`"));
            lines.Add($"- **Entry type:** `{entryTypeName}` — fields: {fieldList}");
        }
        else
        {
            lines.Add($"- **Entry type:** `{entryTypeName}` — static fields (`id`, `name`, `slug`, `status`, `createdAt`, `updatedAt`, `publishedAt`) + `data: {dataTypeName}!`");
        }

        if (dataObjType is not null)
        {
            var dataFieldList = string.Join(", ", dataObjType.Fields.Select(f => $"`{f.Name}: {FormatGraphQLType(f.Type)}`"));
            lines.Add($"- **Data type:** `{dataTypeName}` — fields: {dataFieldList}");
        }
        else
        {
            lines.Add($"- **Data type:** `{dataTypeName}` — one field per field definition above");
        }

        if (createInputType is not null)
        {
            var createFieldList = string.Join(", ", createInputType.Fields.Select(f => $"`{f.Name}: {FormatGraphQLType(f.Type)}`"));
            lines.Add($"- **Create input:** `{createInputName}` — fields: {createFieldList}");
        }
        else
        {
            lines.Add($"- **Create input:** `{createInputName}` — required fields marked with `!`");
        }

        if (updateInputType is not null)
        {
            var updateFieldList = string.Join(", ", updateInputType.Fields.Select(f => $"`{f.Name}: {FormatGraphQLType(f.Type)}`"));
            lines.Add($"- **Update input:** `{updateInputName}` — fields: {updateFieldList}");
        }
        else
        {
            lines.Add($"- **Update input:** `{updateInputName}` — all fields optional");
        }

        lines.Add($"- **Filter input:** `{filterInputName}` — scalar filters via JSONB operators");
        lines.Add($"- **Sort input:** `{sortInputName}` — scalar sorts via JSONB operators");
        lines.Add("");

        // Example Query
        var queryFields = entryType is not null
            ? entryType.Fields.Select(f => f.Name).ToList()
            : new List<string> { "id", "name", "slug", "status", "createdAt", "updatedAt", "publishedAt", "data" };

        var dataFieldNames = collection.Fields.OrderBy(f => f.CreatedAt).Select(f => f.Name).ToList();
        var dataBlock = dataFieldNames.Count == 0
            ? "                # No custom fields defined"
            : string.Join("\n", dataFieldNames.Select(n => $"                {n}"));

        lines.Add("**Example Query**");
        lines.Add("");
        lines.Add("```graphql");
        lines.Add("query {");
        lines.Add("  entries {");
        lines.Add($"    {camel}(first: 10) {{");
        lines.Add("      edges {");
        lines.Add("        node {");
        foreach (var qf in queryFields)
        {
            if (qf == "data")
            {
                lines.Add("          data {");
                lines.Add(dataBlock);
                lines.Add("          }");
            }
            else
            {
                lines.Add($"          {qf}");
            }
        }
        lines.Add("        }");
        lines.Add("      }");
        lines.Add("    }");
        lines.Add("  }");
        lines.Add("}");
        lines.Add("```");
        lines.Add("");

        // Example Create Mutation
        lines.Add("**Example Create Mutation**");
        lines.Add("");
        lines.Add("```graphql");
        lines.Add("mutation {");
        lines.Add("  entries {");
        lines.Add($"    {camel} {{");

        if (mutType is not null && mutType.Fields.FirstOrDefault(f => f.Name == "create") is { } createField)
        {
            lines.Add("      create(");
            foreach (var arg in createField.Arguments)
            {
                if (arg.Name == "data" && UnwrapType(arg.Type) is InputObjectType dataInput)
                {
                    lines.Add("        data: {");
                    foreach (var inputField in dataInput.Fields)
                    {
                        lines.Add($"          {inputField.Name}: <{FormatGraphQLType(inputField.Type)}>");
                    }
                    lines.Add("        }");
                }
                else
                {
                    lines.Add($"        {arg.Name}: <{FormatGraphQLType(arg.Type)}>");
                }
            }
            lines.Add("      ) {");
        }
        else
        {
            lines.Add("      create(");
            lines.Add("        name: \"...\",");
            lines.Add("        slug: \"...\",");
            lines.Add("        locale: En,");
            lines.Add("        data: {");
            var requiredFields = collection.Fields.Where(f => f.IsRequired).OrderBy(f => f.CreatedAt).ToList();
            if (requiredFields.Count == 0)
            {
                lines.Add("          # All fields are optional on create");
            }
            else
            {
                foreach (var field in requiredFields)
                {
                    lines.Add($"          {field.Name}: <{MapFieldDataType(field)}>");
                }
            }
            lines.Add("        }");
            lines.Add("      ) {");
        }

        lines.Add("        id");
        lines.Add("        name");
        lines.Add("        slug");
        lines.Add("        status");
        lines.Add("      }");
        lines.Add("    }");
        lines.Add("  }");
        lines.Add("}");
        lines.Add("```");
        lines.Add("");

        // Example Update Mutation
        lines.Add("**Example Update Mutation**");
        lines.Add("");
        lines.Add("```graphql");
        lines.Add("mutation {");
        lines.Add("  entries {");
        lines.Add($"    {camel} {{");

        if (mutType is not null && mutType.Fields.FirstOrDefault(f => f.Name == "update") is { } updateField)
        {
            lines.Add("      update(");
            foreach (var arg in updateField.Arguments)
            {
                if (arg.Name == "data" && UnwrapType(arg.Type) is InputObjectType dataInput)
                {
                    lines.Add("        data: {");
                    foreach (var inputField in dataInput.Fields)
                    {
                        lines.Add($"          {inputField.Name}: <{FormatGraphQLType(inputField.Type)}>");
                    }
                    lines.Add("        }");
                }
                else
                {
                    lines.Add($"        {arg.Name}: <{FormatGraphQLType(arg.Type)}>");
                }
            }
            lines.Add("      ) {");
        }
        else
        {
            lines.Add("      update(id: \"<uuid>\", data: { /* partial fields */ }) {");
        }

        lines.Add("        id");
        lines.Add("        name");
        lines.Add("      }");
        lines.Add("    }");
        lines.Add("  }");
        lines.Add("}");
        lines.Add("```");
        lines.Add("");

        // Status Transition Mutations
        lines.Add("**Status Transition Mutations**");
        lines.Add("");
        lines.Add("```graphql");
        lines.Add("mutation {");
        lines.Add("  entries {");
        lines.Add($"    {camel} {{");

        if (mutType is not null)
        {
            foreach (var mutField in mutType.Fields.Where(f => f.Name != "create" && f.Name != "update"))
            {
                var args = mutField.Arguments.Select(a => $"{a.Name}: <{FormatGraphQLType(a.Type)}>");
                var argStr = string.Join(", ", args);
                if (!string.IsNullOrEmpty(argStr))
                    argStr = $"({argStr})";
                lines.Add($"      {mutField.Name}{argStr} {{ id status }}");
            }
        }
        else
        {
            lines.Add("      publish(id: \"<uuid>\") { id status }");
            lines.Add("      unpublish(id: \"<uuid>\") { id status }");
            lines.Add("      archive(id: \"<uuid>\") { id status }");
            lines.Add("      restore(id: \"<uuid>\") { id status }");
            lines.Add("      delete(id: \"<uuid>\") { id status }");
        }

        lines.Add("    }");
        lines.Add("  }");
        lines.Add("}");
        lines.Add("```");
        lines.Add("");

        lines.Add("**Filtering & Sorting**");
        lines.Add("");
        lines.Add("Scalar fields support standard filter operators (`eq`, `neq`, `contains`, `startsWith`, `endsWith`, `gt`, `gte`, `lt`, `lte`, `in`, `nin`) and sort directions (`ASC`, `DESC`). Relation fields filter by the target entry UUID and sort by the target entry name.");
        lines.Add("");

        return lines;
    }

    private static string MapFieldDataType(Field field)
    {
        return field.DataType switch
        {
            FieldDataType.Text => "String",
            FieldDataType.Boolean => "Boolean",
            FieldDataType.Number => "Float",
            FieldDataType.DateTime => "DateTime",
            FieldDataType.Relation => "String",
            FieldDataType.Asset => "String",
            FieldDataType.Object => "String",
            _ => "String"
        };
    }

    private static string FormatGraphQLType(IType type)
    {
        return type switch
        {
            NonNullType nonNull => $"{FormatGraphQLType(nonNull.Type)}!",
            ListType list => $"[{FormatGraphQLType(list.ElementType)}]",
            INamedType named => named.Name ?? "Unknown",
            _ => type.ToString() ?? "Unknown"
        };
    }

    private static IType UnwrapType(IType type)
    {
        return type is NonNullType nonNull ? nonNull.Type! : type;
    }
}
