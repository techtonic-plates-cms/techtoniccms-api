using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HotChocolate;
using HotChocolate.Data;
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
/// Query-related type generation for <see cref="CollectionTypeModule"/>.
/// Builds dynamic per-collection entry query fields with paging, filtering, and sorting.
/// </summary>
public partial class CollectionTypeModule
{
    /// <summary>
    /// Builds all query-related GraphQL types: the <c>Entries</c> root type,
    /// per-collection entry types, data types, and the <c>CollectionQuery</c> extension.
    /// </summary>
    private async Task BuildQueryTypesAsync(
        List<Collection> collections,
        Dictionary<Guid, string> collectionTypeMap,
        IDescriptorContext context,
        List<ITypeSystemMember> types,
        CancellationToken cancellationToken)
    {
        // ── Shared entry + data types (used by both queries and mutations) ──
        foreach (var collection in collections)
        {
            var pascalName = DynamicCollectionHelpers.ToPascalCase(collection.Slug);
            var typeName = $"{pascalName}Entry";
            var dataTypeName = $"{pascalName}EntryData";

            var dataTypeDef = DynamicCollectionHelpers.BuildEntryDataTypeDefinition(collection, collectionTypeMap);
            types.Add(ObjectType.CreateUnsafe(dataTypeDef));

            var entryTypeDef = DynamicCollectionHelpers.BuildEntryTypeDefinition(collection, dataTypeName);
            types.Add(ObjectType.CreateUnsafe(entryTypeDef));
        }

        // ── Entries root object type ──
        var collectionEntriesTypeDef = new ObjectTypeDefinition("Entries")
        {
            Description = "Root type for all entries",
            RuntimeType = typeof(Dictionary<string, object>)
        };

        types.Add(ObjectType.CreateUnsafe(collectionEntriesTypeDef));

        // ── Per-collection query fields with paging/filter/sort ──
        foreach (var collection in collections)
        {
            BuildCollectionQueryField(collection, context, collectionEntriesTypeDef);
        }

        // ── Wire entries field onto CollectionQuery root ──
        var queryExtensionDef = new ObjectTypeDefinition("CollectionQuery")
        {
            RuntimeType = typeof(CollectionQuery),
            IsExtension = true
        };

        queryExtensionDef.Fields.Add(new ObjectFieldDefinition(
            "entries",
            "List of all entries for collections",
            TypeReference.Parse("Entries!"),
            resolver: _ => new ValueTask<object?>(new Dictionary<string, object>())));

        types.Add(ObjectTypeExtension.CreateUnsafe(queryExtensionDef));
    }

    /// <summary>
    /// Builds a single per-collection query field with ABAC check, paging, filtering, and sorting.
    /// The field returns <c>IQueryable&lt;Entry&gt;</c> so Hot Chocolate middleware can compose.
    /// </summary>
    private static void BuildCollectionQueryField(
        Collection collection,

        IDescriptorContext context,
        ObjectTypeDefinition collectionEntriesTypeDef)
    {
        var pascalName = DynamicCollectionHelpers.ToPascalCase(collection.Slug);
        var camelName = DynamicCollectionHelpers.ToCamelCase(collection.Slug);
        var typeName = $"{pascalName}Entry";
        var filterTypeName = $"{pascalName}EntryFilterInput";
        var sortTypeName = $"{pascalName}EntrySortInput";
        var collectionId = collection.Id;

        var collectionPropertyDef = new ObjectFieldDefinition(
       camelName,
       $"Access entries from the '{collection.Name}' collection",
       TypeReference.Parse(typeName))
        {
            ResultType = typeof(IQueryable<Entry>),
            Resolver = async ctx =>
            {
                var httpContextAccessor = ctx.Service<IHttpContextAccessor>();
                var readUserId = DynamicCollectionHelpers.GetUserId(httpContextAccessor);
                var readAbacService = ctx.Service<AbacService>();

                await readAbacService.RequirePermissionAsync(
                    readUserId,
                    BaseResource.Entries,
                    PermissionAction.Read,
                    new Dictionary<string, object?>
                    {
                        ["ResourceEntryCollectionId"] = collectionId.ToString()
                    });

                var innerDb = ctx.Service<TechtonicCmsDbContext>();

                return innerDb.Entries
                    .Where(e => e.CollectionId == collectionId)
                    .AsQueryable();
            }
        };
        var fieldDescriptor = collectionPropertyDef.ToDescriptor(context)
            .UsePaging(options: new() { MaxPageSize = 100 }, nodeType: typeof(ObjectType<Entry>),
                connectionName: pascalName + "Entry")
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
                    DynamicCollectionHelpers.AddDynamicFilterField(filterDesc, field);
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
                    DynamicCollectionHelpers.AddDynamicSortField(sortDesc, field);
                }
            });

        

        collectionEntriesTypeDef.Fields.Add(fieldDescriptor.ToDefinition());
    }
}
