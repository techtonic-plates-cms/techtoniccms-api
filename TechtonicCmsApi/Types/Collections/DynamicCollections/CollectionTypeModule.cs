using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using HotChocolate;
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

            dynamicCollectionsTypeDef.Fields.Add(new ObjectFieldDefinition(
                camelName,
                $"Access entries from the '{collection.Name}' collection",
                TypeReference.Parse($"[{typeName}]"),
                resolver: async _ =>
                {
                    var innerScope = _scopeFactory.CreateScope();
                    try
                    {
                        var innerDb = innerScope.ServiceProvider.GetRequiredService<TechtonicCmsDbContext>();
                        var entries = from e in innerDb.Entries
                                      where e.CollectionId == collectionId
                                      orderby e.CreatedAt descending
                                      select e;
                        foreach (var entry in entries)
                        {
                            innerDb.Entry(entry).State = EntityState.Detached;
                        }

                        var es = await entries.ToListAsync(_.RequestAborted);

                        return es;
                    }
                    finally
                    {
                        innerScope.Dispose();
                    }
                }));



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
        FieldDataType.TextList => "[String]",
        FieldDataType.NumberList => "[Float]",
        FieldDataType.Asset => "String",
        FieldDataType.RichText => "String",
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
}
