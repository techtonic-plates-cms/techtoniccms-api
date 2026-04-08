using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using HotChocolate;
using HotChocolate.Data;
using HotChocolate.Data.Filters;
using HotChocolate.Data.Sorting;
using HotChocolate.Execution.Configuration;
using HotChocolate.Types;
using HotChocolate.Types.Descriptors;
using HotChocolate.Types.Descriptors.Definitions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TechtonicCmsApi.Contexts;
using TechtonicCmsApi.Schema.TechtonicCms.Entities;
using TechtonicCmsApi.Schema.TechtonicCms.Enums;

namespace TechtonicCmsApi.Types.Collections.DynamicCollections;

public class CollectionTypeModule : TypeModule
{
    private readonly IServiceScopeFactory _scopeFactory;

    public CollectionTypeModule(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public void TriggerTypesChanged() => OnTypesChanged();

    public override async ValueTask<IReadOnlyCollection<ITypeSystemMember>> CreateTypesAsync(
        IDescriptorContext context,
        CancellationToken cancellationToken)
    {
        var types = new List<ITypeSystemMember>();

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TechtonicCmsDbContext>();

        var collections = await db.Collections
            .Include(c => c.Fields)
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);

        if (collections.Count == 0)
            return types;

        var queryExtensionDef = new ObjectTypeDefinition("CollectionQuery")
        {
            RuntimeType = typeof(CollectionQuery),
            IsExtension = true
        };

        var dynamicCollectionsTypeDef = new ObjectTypeDefinition("DynamicCollections")
        {
            Description = "Root type for all dynamic collections",
            RuntimeType = typeof(Dictionary<string, object>)
        };

        types.Add(ObjectType.CreateUnsafe(dynamicCollectionsTypeDef));

        foreach (var collection in collections)
        {
            var pascalName = ToPascalCase(collection.Slug);
            var camelName = ToCamelCase(collection.Slug);
            var typeName = $"{pascalName}Entry";
            var dataTypeName = $"{pascalName}EntryData";
            var collectionId = collection.Id;

            var dataTypeDef = new ObjectTypeDefinition(dataTypeName)
            {
                Description = $"Dynamic data type for the '{collection.Name}' collection",
                RuntimeType = typeof(Dictionary<string, object>)
            };

            foreach (var field in collection.Fields.OrderBy(f => f.CreatedAt))
            {
                var graphqlType = MapFieldType(field.DataType);
                dataTypeDef.Fields.Add(new ObjectFieldDefinition(
                    field.Name,
                    field.Description,
                    TypeReference.Parse(graphqlType),
                    pureResolver: ctx =>
                        ctx.Parent<Dictionary<string, object>>().GetValueOrDefault(field.Name)));
            }

            types.Add(ObjectType.CreateUnsafe(dataTypeDef));

            var entryTypeDef = new ObjectTypeDefinition(typeName)
            {
                Description = $"Dynamic entry type for the '{collection.Name}' collection",
                RuntimeType = typeof(Entry)
            };

            entryTypeDef.Fields.Add(new ObjectFieldDefinition(
                        "id",
                        "Unique identifier",
                        TypeReference.Parse("ID!"),
                        pureResolver: ctx => ctx.Parent<Entry>().Id));

            entryTypeDef.Fields.Add(new ObjectFieldDefinition(
                "name",
                "Entry name",
                TypeReference.Parse("String!"),
                pureResolver: ctx => ctx.Parent<Entry>().Name));

            entryTypeDef.Fields.Add(new ObjectFieldDefinition(
                "slug",
                "URL-friendly identifier",
                TypeReference.Parse("String"),
                pureResolver: ctx =>
                    ctx.Parent<Entry>().Slug));

            entryTypeDef.Fields.Add(new ObjectFieldDefinition(
                "status",
                "Entry status",
                TypeReference.Parse("String!"),
                pureResolver: ctx => ctx.Parent<Entry>().Status));

            entryTypeDef.Fields.Add(new ObjectFieldDefinition(
                "createdAt",
                "Creation timestamp",
                TypeReference.Parse("DateTime!"),
                pureResolver: ctx => ctx.Parent<Entry>().CreatedAt));

            entryTypeDef.Fields.Add(new ObjectFieldDefinition(
                "updatedAt",
                "Last update timestamp",
                TypeReference.Parse("DateTime!"),
                pureResolver: ctx => ctx.Parent<Entry>().UpdatedAt));

            entryTypeDef.Fields.Add(new ObjectFieldDefinition(
                "publishedAt",
                "Publication timestamp",
                TypeReference.Parse("DateTime"),
                pureResolver: ctx =>
                    ctx.Parent<Entry>().PublishedAt));


            entryTypeDef.Fields.Add(new ObjectFieldDefinition(
                "data",
                $"Dynamic data for the '{collection.Name}' collection",
                TypeReference.Parse($"{dataTypeName}!"),
                resolver: _ =>
                {
                    var entry = _.Parent<Entry>();
                    var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(entry.Data.RootElement.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    return new ValueTask<object?>(dict ?? new Dictionary<string, object>());
                }
            ));

            types.Add(ObjectType.CreateUnsafe(entryTypeDef));

            var filterTypeName = $"{pascalName}EntryFilterInput";
            var sortTypeName = $"{pascalName}EntrySortInput";

            var collectionPropertyDef = new ObjectFieldDefinition(
                camelName,
                $"Access entries from the '{collection.Name}' collection",
                TypeReference.Parse($"[{typeName}]"),

                // Return IQueryable<Entry> so HC filter/sort/paging middleware can compose
                resolver: _ =>
                {
                    var innerScope = _scopeFactory.CreateScope();
                    var innerDb = innerScope.ServiceProvider.GetRequiredService<TechtonicCmsDbContext>();

                    IQueryable<Entry> entries = innerDb.Entries
                        .Where(e => e.CollectionId == collectionId);

                    return new ValueTask<object?>(entries);
                });

            var fieldDescriptor = collectionPropertyDef.ToDescriptor(context)
                .UsePaging(options: new() { MaxPageSize = 100 })
                .UseFiltering<Entry>(filterDesc =>
                {
                    filterDesc.BindFieldsExplicitly();
                    filterDesc.Name(filterTypeName);

                    // Static entry fields
                    filterDesc.Field(e => e.Name);
                    filterDesc.Field(e => e.Slug);
                    filterDesc.Field(e => e.Status);
                    filterDesc.Field(e => e.CreatedAt);
                    filterDesc.Field(e => e.UpdatedAt);
                    filterDesc.Field(e => e.PublishedAt);

                    // Dynamic jsonb fields
                    foreach (var field in collection.Fields.OrderBy(f => f.CreatedAt))
                    {
                        AddDynamicFilterField(filterDesc, field);
                    }
                })
                .UseSorting<Entry>(sortDesc =>
                {
                    sortDesc.BindFieldsExplicitly();
                    sortDesc.Name(sortTypeName);

                    // Static entry fields
                    sortDesc.Field(e => e.Name);
                    sortDesc.Field(e => e.Slug);
                    sortDesc.Field(e => e.Status);
                    sortDesc.Field(e => e.CreatedAt);
                    sortDesc.Field(e => e.UpdatedAt);
                    sortDesc.Field(e => e.PublishedAt);

                    // Dynamic jsonb fields
                    foreach (var field in collection.Fields.OrderBy(f => f.CreatedAt))
                    {
                        AddDynamicSortField(sortDesc, field);
                    }
                });

            dynamicCollectionsTypeDef.Fields.Add(fieldDescriptor.ToDefinition());


        }

        queryExtensionDef.Fields.Add(new ObjectFieldDefinition(
            "dynamicCollections",
            "List of all dynamic collections",
            TypeReference.Parse("DynamicCollections!"),
            resolver: _ => new ValueTask<object?>(new[] { new Dictionary<string, object>() })
        ));

        types.Add(ObjectTypeExtension.CreateUnsafe(queryExtensionDef));

        return types;
    }

    private static void AddMetadataFields(ObjectTypeDefinition typeDef)
    {

    }

    private static string MapFieldType(FieldDataType dataType) => dataType switch
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

    private static string ToPascalCase(string slug)
    {
        return string.Concat(slug
            .Split('-', '_')
            .Select(part => char.ToUpper(part[0]) + part[1..].ToLower()));
    }

    private static string ToCamelCase(string slug)
    {
        var parts = slug.Split('-', '_');
        return parts[0].ToLower() + string.Concat(
            parts[1..].Select(part => char.ToUpper(part[0]) + part[1..].ToLower()));
    }

    /// <summary>
    /// Adds a dynamic jsonb field to the filter descriptor based on the field's <see cref="FieldDataType"/>.
    /// Uses <see cref="CmsDbFunctions"/> methods as lambda expressions so EF Core translates them to
    /// PostgreSQL <c>cms_extract_*</c> SQL functions.
    /// </summary>
    private static void AddDynamicFilterField(IFilterInputTypeDescriptor<Entry> filterDesc, Field field)
    {
        var fieldName = field.Name;

        switch (field.DataType)
        {
            case FieldDataType.Text:
            case FieldDataType.Asset:
            case FieldDataType.Object:
            case FieldDataType.Relation:
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
            default:
                break;
        }
    }

    /// <summary>
    /// Adds a dynamic jsonb field to the sort descriptor based on the field's <see cref="FieldDataType"/>.
    /// Uses <see cref="CmsDbFunctions"/> methods as lambda expressions so EF Core translates them to
    /// PostgreSQL <c>cms_extract_*</c> SQL functions.
    /// </summary>
    private static void AddDynamicSortField(ISortInputTypeDescriptor<Entry> sortDesc, Field field)
    {
        var fieldName = field.Name;

        switch (field.DataType)
        {
            case FieldDataType.Text:
            case FieldDataType.Asset:
            case FieldDataType.Object:
            case FieldDataType.Relation:
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
            default:
                break;
        }
    }
}
