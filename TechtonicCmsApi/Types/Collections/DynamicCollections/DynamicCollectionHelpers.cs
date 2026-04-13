using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using HotChocolate;
using HotChocolate.Data.Filters;
using HotChocolate.Data.Sorting;
using HotChocolate.Types;
using HotChocolate.Types.Descriptors;
using HotChocolate.Types.Descriptors.Definitions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using TechtonicCmsApi.Contexts;
using TechtonicCmsApi.Schema.TechtonicCms.Entities;
using TechtonicCmsApi.Schema.TechtonicCms.Enums;
using TechtonicCmsApi.Services;

namespace TechtonicCmsApi.Types.Collections.DynamicCollections;

/// <summary>
/// Static utility methods shared between the query and mutation partials
/// of <see cref="CollectionTypeModule"/>.
/// </summary>
internal static class DynamicCollectionHelpers
{
    // ──────────────────────────────────────────────────────────────────
    // Type Naming
    // ──────────────────────────────────────────────────────────────────

    public static string MapFieldType(FieldDataType dataType) => dataType switch
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

    public static string ToPascalCase(string slug)
    {
        return string.Concat(slug
            .Split('-', '_')
            .Select(part => char.ToUpper(part[0]) + part[1..].ToLower()));
    }

    public static string ToCamelCase(string slug)
    {
        var parts = slug.Split('-', '_');
        return parts[0].ToLower() + string.Concat(
            parts[1..].Select(part => char.ToUpper(part[0]) + part[1..].ToLower()));
    }

    // ──────────────────────────────────────────────────────────────────
    // Authentication
    // ──────────────────────────────────────────────────────────────────

    public static Guid GetUserId(IHttpContextAccessor httpContextAccessor)
    {
        var user = httpContextAccessor.HttpContext?.User;
        if (user is null)
            throw new GraphQLException(ErrorBuilder.New()
                .SetMessage("No authenticated user found")
                .SetCode("UNAUTHENTICATED")
                .Build());

        var userIdClaim = user.FindFirst("userId")?.Value
            ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
            throw new GraphQLException(ErrorBuilder.New()
                .SetMessage("Invalid or missing user identity")
                .SetCode("UNAUTHENTICATED")
                .Build());

        return userId;
    }

    // ──────────────────────────────────────────────────────────────────
    // Slug Generation
    // ──────────────────────────────────────────────────────────────────

    public static string GenerateSlug(string name)
    {
        var slug = name.Trim().ToLowerInvariant();
        slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[^a-z0-9]+", "-");
        slug = System.Text.RegularExpressions.Regex.Replace(slug, @"^-+|-+$", "");
        slug = slug.Trim('-');

        if (string.IsNullOrWhiteSpace(slug))
            slug = Guid.NewGuid().ToString("N")[..8];

        return slug;
    }

    // ──────────────────────────────────────────────────────────────────
    // Validation
    // ──────────────────────────────────────────────────────────────────

    public static async Task ValidateEntryData(
        Dictionary<string, object?> scalarData,
        Dictionary<Guid, string> relationValues,
        List<Field> collectionFields,
        TechtonicCmsDbContext db,
        Guid collectionId,
        Guid? excludeEntryId)
    {
        // Validate unique constraints on scalar fields
        var uniqueFields = collectionFields.Where(f => f.IsUnique && f.DataType != FieldDataType.Relation).ToList();
        foreach (var field in uniqueFields)
        {
            if (!scalarData.TryGetValue(field.Name, out var value) || value is null)
                continue;

            var valueStr = value.ToString()!;
            var query = db.Entries.Where(e =>
                e.CollectionId == collectionId &&
                e.Data.RootElement.GetProperty(field.Name).GetString() == valueStr);

            if (excludeEntryId.HasValue)
                query = query.Where(e => e.Id != excludeEntryId.Value);

            if (await query.AnyAsync())
                throw new GraphQLException(ErrorBuilder.New()
                    .SetMessage($"Value '{valueStr}' for field '{field.Name}' already exists in this collection")
                    .SetCode("CONFLICT")
                    .Build());
        }

        // Validate relation targets exist in their respective collections
        var relationFields = collectionFields.Where(f =>
            f.DataType == FieldDataType.Relation && f.RelatedCollectionId.HasValue).ToList();

        foreach (var field in relationFields)
        {
            if (!relationValues.TryGetValue(field.Id, out var relIdStr))
                continue;

            if (!Guid.TryParse(relIdStr, out var relId))
                throw new GraphQLException(ErrorBuilder.New()
                    .SetMessage($"Invalid relation ID '{relIdStr}' for field '{field.Name}'")
                    .SetCode("BAD_REQUEST")
                    .Build());

            var relatedExists = await db.Entries.AnyAsync(e =>
                e.Id == relId && e.CollectionId == field.RelatedCollectionId!.Value);

            if (!relatedExists)
                throw new GraphQLException(ErrorBuilder.New()
                    .SetMessage($"Related entry '{relIdStr}' not found in collection")
                    .SetCode("NOT_FOUND")
                    .Build());
        }
    }

    // ──────────────────────────────────────────────────────────────────
    // Dynamic Filter & Sort Fields
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Adds a dynamic field to the filter descriptor based on the field's <see cref="FieldDataType"/>.
    /// Scalar fields use <see cref="CmsDbFunctions"/> JSONB extraction; relation fields use
    /// <see cref="Entry.FromRelations"/> navigation property subqueries.
    /// </summary>
    public static void AddDynamicFilterField(IFilterInputTypeDescriptor<Entry> filterDesc, Field field)
    {
        var fieldName = field.Name;

        switch (field.DataType)
        {
            case FieldDataType.Text:
            case FieldDataType.Asset:
            case FieldDataType.Object:
                filterDesc.Field(e => CmsDbFunctions.CmsExtractText(e.Data, fieldName))
                    .Name(fieldName);
                break;

            case FieldDataType.Boolean:
                filterDesc.Field(e => CmsDbFunctions.CmsExtractBoolean(e.Data, fieldName))
                    .Name(fieldName);
                break;

            case FieldDataType.Number:
                filterDesc.Field(e => CmsDbFunctions.CmsExtractNumber(e.Data, fieldName))
                    .Name(fieldName);
                break;

            case FieldDataType.DateTime:
                filterDesc.Field(e => CmsDbFunctions.CmsExtractDateTime(e.Data, fieldName))
                    .Name(fieldName);
                break;

            case FieldDataType.Relation:
                var relFieldId = field.Id;
                filterDesc.Field(e => e.FromRelations
                        .Where(r => r.FieldId == relFieldId)
                        .Select(r => r.TargetEntryId)
                        .FirstOrDefault())
                    .Name(fieldName);
                break;

            default:
                break;
        }
    }

    /// <summary>
    /// Adds a dynamic field to the sort descriptor based on the field's <see cref="FieldDataType"/>.
    /// Scalar fields use <see cref="CmsDbFunctions"/> JSONB extraction; relation fields sort by
    /// target entry name via <see cref="Entry.FromRelations"/> navigation property subqueries.
    /// </summary>
    public static void AddDynamicSortField(ISortInputTypeDescriptor<Entry> sortDesc, Field field)
    {
        var fieldName = field.Name;

        switch (field.DataType)
        {
            case FieldDataType.Text:
            case FieldDataType.Asset:
            case FieldDataType.Object:
                sortDesc.Field(e => CmsDbFunctions.CmsExtractText(e.Data, fieldName))
                    .Name(fieldName);
                break;

            case FieldDataType.Boolean:
                sortDesc.Field(e => CmsDbFunctions.CmsExtractBoolean(e.Data, fieldName))
                    .Name(fieldName);
                break;

            case FieldDataType.Number:
                sortDesc.Field(e => CmsDbFunctions.CmsExtractNumber(e.Data, fieldName))
                    .Name(fieldName);
                break;

            case FieldDataType.DateTime:
                sortDesc.Field(e => CmsDbFunctions.CmsExtractDateTime(e.Data, fieldName))
                    .Name(fieldName);
                break;

            case FieldDataType.Relation:
                var relFieldId = field.Id;
                sortDesc.Field(e => e.FromRelations
                        .Where(r => r.FieldId == relFieldId)
                        .Select(r => r.TargetEntry.Name)
                        .FirstOrDefault())
                    .Name(fieldName);
                break;

            default:
                break;
        }
    }

    // ──────────────────────────────────────────────────────────────────
    // Type Definition Builders
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds the dynamic <c>{PascalName}EntryData</c> object type definition that maps
    /// collection field definitions to GraphQL scalar/relation fields.
    /// </summary>
    public static ObjectTypeDefinition BuildEntryDataTypeDefinition(
        Collection collection,
        Dictionary<Guid, string> collectionTypeMap)
    {
        var pascalName = ToPascalCase(collection.Slug);
        var dataTypeName = $"{pascalName}EntryData";

        var dataTypeDef = new ObjectTypeDefinition(dataTypeName)
        {
            Description = $"Dynamic data type for the '{collection.Name}' collection",
            RuntimeType = typeof(Dictionary<string, object>)
        };

        foreach (var field in collection.Fields.OrderBy(f => f.CreatedAt))
        {
            if (field.DataType == FieldDataType.Relation
                && field.RelatedCollectionId.HasValue
                && collectionTypeMap.TryGetValue(field.RelatedCollectionId.Value, out var relationTypeName))
            {
                var fieldId = field.Id;
                var fieldName = field.Name;

                dataTypeDef.Fields.Add(new ObjectFieldDefinition(
                    field.Name,
                    field.Description,
                    TypeReference.Parse(relationTypeName),
                    resolver: async ctx =>
                    {
                        var data = ctx.Parent<Dictionary<string, object>>();

                        if (!data.ContainsKey(fieldName))
                            return null;

                        if (!data.TryGetValue("__entryId", out var rawEntryId) || rawEntryId is not Guid entryId)
                            return null;

                        var relationDb = ctx.Service<TechtonicCmsDbContext>();

                        var relation = await relationDb.EntryRelations
                            .Where(r => r.EntryId == entryId && r.FieldId == fieldId)
                            .Include(r => r.TargetEntry)
                            .FirstOrDefaultAsync();

                        return relation?.TargetEntry;
                    }));
            }
            else
            {
                var graphqlType = MapFieldType(field.DataType);
                dataTypeDef.Fields.Add(new ObjectFieldDefinition(
                    field.Name,
                    field.Description,
                    TypeReference.Parse(graphqlType),
                    pureResolver: ctx =>
                        ctx.Parent<Dictionary<string, object>>().GetValueOrDefault(field.Name)));
            }
        }

        return dataTypeDef;
    }

    /// <summary>
    /// Builds the <c>{PascalName}Entry</c> object type definition with static entry fields
    /// (id, name, slug, status, timestamps) and a dynamic <c>data</c> field.
    /// </summary>
    public static ObjectTypeDefinition BuildEntryTypeDefinition(
        Collection collection,
        string dataTypeName)
    {
        var pascalName = ToPascalCase(collection.Slug);
        var typeName = $"{pascalName}Entry";

        var entryTypeDef = new ObjectTypeDefinition(typeName)
        {
            Description = $"Dynamic entry type for the '{collection.Name}' collection",
            RuntimeType = typeof(Entry)
        };

        entryTypeDef.Fields.Add(new ObjectFieldDefinition(
            "id", "Unique identifier", TypeReference.Parse("ID!"),
            pureResolver: ctx => ctx.Parent<Entry>().Id));

        entryTypeDef.Fields.Add(new ObjectFieldDefinition(
            "name", "Entry name", TypeReference.Parse("String!"),
            pureResolver: ctx => ctx.Parent<Entry>().Name));

        entryTypeDef.Fields.Add(new ObjectFieldDefinition(
            "slug", "URL-friendly identifier", TypeReference.Parse("String"),
            pureResolver: ctx => ctx.Parent<Entry>().Slug));

        entryTypeDef.Fields.Add(new ObjectFieldDefinition(
            "status", "Entry status", TypeReference.Parse("EntryStatus!"),
            pureResolver: ctx => ctx.Parent<Entry>().Status));

        entryTypeDef.Fields.Add(new ObjectFieldDefinition(
            "createdAt", "Creation timestamp", TypeReference.Parse("DateTime!"),
            pureResolver: ctx => ctx.Parent<Entry>().CreatedAt));

        entryTypeDef.Fields.Add(new ObjectFieldDefinition(
            "updatedAt", "Last update timestamp", TypeReference.Parse("DateTime!"),
            pureResolver: ctx => ctx.Parent<Entry>().UpdatedAt));

        entryTypeDef.Fields.Add(new ObjectFieldDefinition(
            "publishedAt", "Publication timestamp", TypeReference.Parse("DateTime"),
            pureResolver: ctx => ctx.Parent<Entry>().PublishedAt));

        entryTypeDef.Fields.Add(new ObjectFieldDefinition(
            "data",
            $"Dynamic data for the '{collection.Name}' collection",
            TypeReference.Parse($"{dataTypeName}!"),
            resolver: ctx =>
            {
                var entry = ctx.Parent<Entry>();
                var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(
                        entry.Data.RootElement.GetRawText(),
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                    ?? new Dictionary<string, object>();

                // Inject entry ID so relation field resolvers can query entry_relations
                dict["__entryId"] = entry.Id;

                return new ValueTask<object?>(dict);
            }));

        return entryTypeDef;
    }

    /// <summary>
    /// Builds the <c>{PascalName}CreateEntryDataInput</c> and
    /// <c>{PascalName}UpdateEntryDataInput</c> input type definitions for a collection.
    /// </summary>
    public static (InputObjectTypeDefinition createDataInputDef, InputObjectTypeDefinition updateDataInputDef)
        BuildInputTypeDefinitions(Collection collection)
    {
        var pascalName = ToPascalCase(collection.Slug);
        var createDataInputTypeName = $"{pascalName}CreateEntryDataInput";
        var updateDataInputTypeName = $"{pascalName}UpdateEntryDataInput";
        var fields = collection.Fields.OrderBy(f => f.CreatedAt).ToList();

        var createDataInputDef = new InputObjectTypeDefinition(createDataInputTypeName)
        {
            Description = $"Input type for creating entries in the '{collection.Name}' collection",
            RuntimeType = typeof(Dictionary<string, object>)
        };

        var updateDataInputDef = new InputObjectTypeDefinition(updateDataInputTypeName)
        {
            Description = $"Input type for updating entries in the '{collection.Name}' collection (all fields optional)",
            RuntimeType = typeof(Dictionary<string, object>)
        };

        foreach (var field in fields)
        {
            var graphqlType = MapFieldType(field.DataType);

            // Create input: required fields get !
            var createFieldType = field.IsRequired ? graphqlType + "!" : graphqlType;
            createDataInputDef.Fields.Add(new InputFieldDefinition(
                field.Name,
                field.Description ?? $"Field '{field.Name}'",
                TypeReference.Parse(createFieldType)));

            // Update input: all fields optional (partial update)
            updateDataInputDef.Fields.Add(new InputFieldDefinition(
                field.Name,
                field.Description ?? $"Field '{field.Name}'",
                TypeReference.Parse(graphqlType)));
        }

        return (createDataInputDef, updateDataInputDef);
    }
}
