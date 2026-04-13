using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HotChocolate;
using HotChocolate.Execution.Configuration;
using HotChocolate.Types.Descriptors;
using Microsoft.EntityFrameworkCore;
using TechtonicCmsApi.Contexts;
using TechtonicCmsApi.Schema.TechtonicCms.Entities;

namespace TechtonicCmsApi.Types.Collections.DynamicCollections;

/// <summary>
/// Hot Chocolate <see cref="TypeModule"/> that dynamically generates GraphQL types
/// at runtime from database-defined collections. Delegates to:
/// <list type="bullet">
///   <item><c>.Queries</c> partial — entry query fields with paging/filter/sort</item>
///   <item><c>.Mutations</c> partial — entry CRUD mutations</item>
///   <item><see cref="DynamicCollectionHelpers"/> — shared utilities and type builders</item>
/// </list>
/// </summary>
public partial class CollectionTypeModule : TypeModule
{
    public CollectionTypeModule() { }

    public void TriggerTypesChanged() => OnTypesChanged();

    public override async ValueTask<IReadOnlyCollection<ITypeSystemMember>> CreateTypesAsync(
        IDescriptorContext context,
        CancellationToken cancellationToken)
    {
        var types = new List<ITypeSystemMember>();

        var db = context.Services
            .GetRequiredService<IDbContextFactory<TechtonicCmsDbContext>>()
            .CreateDbContext();

        var collections = await db.Collections
            .Include(c => c.Fields)
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);

        if (collections.Count == 0)
            return types;

        // Map collection ID → entry type name for relation field resolution
        var collectionTypeMap = collections.ToDictionary(
            c => c.Id,
            c => $"{DynamicCollectionHelpers.ToPascalCase(c.Slug)}Entry");

        // Build query types (entry types, data types, query fields)
        await BuildQueryTypesAsync(collections, collectionTypeMap, context, types, cancellationToken);

        // Build mutation types (input types, CRUD resolvers, mutation wiring)
        BuildMutationTypes(collections, types);

        return types;
    }
}
