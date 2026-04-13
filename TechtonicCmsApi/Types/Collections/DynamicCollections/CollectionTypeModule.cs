using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using HotChocolate;
using HotChocolate.Authorization;
using HotChocolate.Data;
using HotChocolate.Data.Filters;
using HotChocolate.Data.Sorting;
using HotChocolate.Execution.Configuration;
using HotChocolate.Resolvers;
using HotChocolate.Types.Descriptors;
using HotChocolate.Types.Descriptors.Definitions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TechtonicCmsApi.Contexts;
using TechtonicCmsApi.Schema.TechtonicCms.Entities;
using TechtonicCmsApi.Schema.TechtonicCms.Enums;
using TechtonicCmsApi.Services;

namespace TechtonicCmsApi.Types.Collections.DynamicCollections;

public class CollectionTypeModule : TypeModule
{

    public CollectionTypeModule()
    {
    }

    public void TriggerTypesChanged() => OnTypesChanged();

    public override async ValueTask<IReadOnlyCollection<ITypeSystemMember>> CreateTypesAsync(
        IDescriptorContext context,
        CancellationToken cancellationToken)
    {
        var types = new List<ITypeSystemMember>();

        var db = context.Services.GetRequiredService<IDbContextFactory<TechtonicCmsDbContext>>().CreateDbContext();

        var collections = await db.Collections
            .Include(c => c.Fields)
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);

        if (collections.Count == 0)
            return types;

        // Map collection ID → entry type name for relation field resolution
        var collectionTypeMap = collections.ToDictionary(
            c => c.Id,
            c => $"{ToPascalCase(c.Slug)}Entry");

        var queryExtensionDef = new ObjectTypeDefinition("CollectionQuery")
        {
            RuntimeType = typeof(CollectionQuery),
            IsExtension = true
        };

        var collectionEntriesTypeDef = new ObjectTypeDefinition("Entries")
        {
            Description = "Root type for all entries",
            RuntimeType = typeof(Dictionary<string, object>)
        };

        types.Add(ObjectType.CreateUnsafe(collectionEntriesTypeDef));

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
                if (field.DataType == FieldDataType.Relation
                    && field.RelatedCollectionId.HasValue
                    && collectionTypeMap.TryGetValue(field.RelatedCollectionId.Value, out var relationTypeName))
                {
                    // Relation field: resolve via entry_relations join table
                    var fieldId = field.Id;
                    var fieldName = field.Name;
                    var capturedField = field;
                    dataTypeDef.Fields.Add(new ObjectFieldDefinition(
                        field.Name,
                        field.Description,
                        TypeReference.Parse(relationTypeName),
                        resolver: async ctx =>
                        {
                            var data = ctx.Parent<Dictionary<string, object>>();

                            // If the data resolver already stripped this field, return null
                            if (!data.ContainsKey(fieldName))
                                return null;

                            // The __entryId key is injected by the data resolver below
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
                TypeReference.Parse("EntryStatus!"),
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
                resolver: async ctx =>
                {
                    var entry = ctx.Parent<Entry>();
                    var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(entry.Data.RootElement.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                        ?? new Dictionary<string, object>();

                    // Inject entry ID so relation field resolvers can query entry_relations
                    dict["__entryId"] = entry.Id;

                    // Field-level ABAC: strip fields the current user cannot read
                    var httpContextAccessor = ctx.Service<IHttpContextAccessor>();
                    var userId = GetUserId(httpContextAccessor);
                    var abacService = ctx.Service<AbacService>();

                    var accessibleFieldNames = (await abacService.FilterAccessibleFieldsAsync(
                        userId,
                        collection.Fields.OrderBy(f => f.CreatedAt).ToList(),
                        PermissionAction.Read))
                        .Select(f => f.Name)
                        .ToHashSet();

                    // Also keep the internal __entryId key
                    var filtered = new Dictionary<string, object>();
                    foreach (var kvp in dict)
                    {
                        if (kvp.Key == "__entryId" || accessibleFieldNames.Contains(kvp.Key))
                            filtered[kvp.Key] = kvp.Value;
                    }

                    return filtered;
                }
            ));

            types.Add(ObjectType.CreateUnsafe(entryTypeDef));

            var filterTypeName = $"{pascalName}EntryFilterInput";
            var sortTypeName = $"{pascalName}EntrySortInput";

            var collectionPropertyDef = new ObjectFieldDefinition(
                camelName,
                $"Access entries from the '{collection.Name}' collection",
                TypeReference.Parse(typeName), // Define it as a single type, this is necessary for paging to correctly wrap itself around our resolver

                // Return IQueryable<Entry> so HC filter/sort/paging middleware can compose
                resolver: ctx =>
                {
                    var innerDb = ctx.Service<TechtonicCmsDbContext>();
                    IQueryable<Entry> entries = innerDb.Entries
                        .Where(e => e.CollectionId == collectionId);

                    return new ValueTask<object?>(entries);
                })
            {
                ResultType = typeof(IQueryable<Entry>)
            };

            var fieldDescriptor = collectionPropertyDef.ToDescriptor(context)
                     .UsePaging(options: new() { MaxPageSize = 100 }, nodeType: typeof(ObjectType<Entry>),
    connectionName: pascalName + "Entry")
                //.UseProjection()
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

            collectionEntriesTypeDef.Fields.Add(fieldDescriptor.ToDefinition());


        }

        queryExtensionDef.Fields.Add(new ObjectFieldDefinition(
            "entries",
            "List of all entries for collections",
            TypeReference.Parse("Entries!"),
            resolver: _ => new ValueTask<object?>(new Dictionary<string, object>())
        ));

        types.Add(ObjectTypeExtension.CreateUnsafe(queryExtensionDef));

        // ──────────────────────────────────────────────────────────────
        // Dynamic Mutation Types
        // ──────────────────────────────────────────────────────────────

        var entriesMutationsTypeDef = new ObjectTypeDefinition("EntriesMutations")
        {
            Description = "Root type for all entry mutations",
            RuntimeType = typeof(Dictionary<string, object>)
        };

        types.Add(ObjectType.CreateUnsafe(entriesMutationsTypeDef));

        foreach (var collection in collections)
        {
            var pascalName = ToPascalCase(collection.Slug);
            var camelName = ToCamelCase(collection.Slug);
            var collectionMutationsTypeName = $"{pascalName}Mutations";
            var entryTypeName = $"{pascalName}Entry";
            var collectionId = collection.Id;
            var fields = collection.Fields.OrderBy(f => f.CreatedAt).ToList();

            // ── Per-collection mutation sub-type ──
            var collectionMutationsTypeDef = new ObjectTypeDefinition(collectionMutationsTypeName)
            {
                Description = $"Mutations for the '{collection.Name}' collection",
                RuntimeType = typeof(Dictionary<string, object>)
            };

            // ── Dynamic input types ──
            var createDataInputTypeName = $"{pascalName}CreateEntryDataInput";
            var updateDataInputTypeName = $"{pascalName}UpdateEntryDataInput";

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

            types.Add(InputObjectType.CreateUnsafe(createDataInputDef));
            types.Add(InputObjectType.CreateUnsafe(updateDataInputDef));

            // ── create mutation ──
            var createFieldDef = new ObjectFieldDefinition(
                "create",
                $"Create a new entry in the '{collection.Name}' collection",
                TypeReference.Parse($"{entryTypeName}!"));

            createFieldDef.Arguments.Add(new ArgumentDefinition(
                "name", "Entry name", TypeReference.Parse("String!")));

            createFieldDef.Arguments.Add(new ArgumentDefinition(
                "slug", "URL-friendly identifier (auto-generated if omitted)", TypeReference.Parse("String")));

            createFieldDef.Arguments.Add(new ArgumentDefinition(
                "locale", "Entry locale (defaults to collection default)", TypeReference.Parse("Locale")));

            createFieldDef.Arguments.Add(new ArgumentDefinition(
                "status", "Entry status (defaults to DRAFT)", TypeReference.Parse("EntryStatus")));

            createFieldDef.Arguments.Add(new ArgumentDefinition(
                "data", $"Dynamic data for the '{collection.Name}' collection",
                TypeReference.Parse($"{createDataInputTypeName}!")));

            createFieldDef.Resolver = async ctx =>
            {
                var mutationDb = ctx.Service<TechtonicCmsDbContext>();
                var abacService = ctx.Service<AbacService>();
                var httpContextAccessor = ctx.Service<IHttpContextAccessor>();

                var userId = GetUserId(httpContextAccessor);
                await abacService.RequirePermissionAsync(userId, BaseResource.Entries, PermissionAction.Create);

                var name = ctx.ArgumentValue<string>("name");
                var slug = ctx.ArgumentValue<string>("slug");
                var localeArg = ctx.ArgumentValue<Locale?>("locale");
                var statusArg = ctx.ArgumentValue<EntryStatus?>("status");
                var data = ctx.ArgumentValue<Dictionary<string, object?>>("data")
                    ?? new Dictionary<string, object?>();

                // Load collection fields for validation
                var collectionFields = await mutationDb.Fields
                    .Where(f => f.CollectionId == collectionId)
                    .ToListAsync();

                // Field-level ABAC: determine which fields the user can write to
                var writableFields = await abacService.FilterAccessibleFieldsAsync(
                    userId, collectionFields, PermissionAction.Update);
                var writableFieldNames = writableFields.Select(f => f.Name).ToHashSet();
                var writableFieldIds = writableFields.Select(f => f.Id).ToHashSet();

                // Separate scalar vs relation field values from the nested data input
                var scalarData = new Dictionary<string, object?>();
                var relationValues = new Dictionary<Guid, string>(); // fieldId → target entry ID string

                foreach (var f in collectionFields)
                {
                    if (!data.TryGetValue(f.Name, out var argVal))
                    {
                        // Check required fields
                        if (f.IsRequired)
                        {
                            throw new GraphQLException(ErrorBuilder.New()
                                .SetMessage($"Required field '{f.Name}' is missing")
                                .SetCode("BAD_REQUEST")
                                .Build());
                        }
                        continue;
                    }

                    // Skip fields the user doesn't have permission to write
                    if (argVal is not null && !writableFieldNames.Contains(f.Name))
                    {
                        throw new GraphQLException(ErrorBuilder.New()
                            .SetMessage($"Permission denied: cannot write to field '{f.Name}'")
                            .SetCode("FORBIDDEN")
                            .Build());
                    }

                    if (f.DataType == FieldDataType.Relation)
                    {
                        // Relation values go to the join table, not JSONB
                        if (argVal is not null)
                        {
                            if (!writableFieldIds.Contains(f.Id))
                            {
                                throw new GraphQLException(ErrorBuilder.New()
                                    .SetMessage($"Permission denied: cannot write to field '{f.Name}'")
                                    .SetCode("FORBIDDEN")
                                    .Build());
                            }
                            relationValues[f.Id] = argVal.ToString()!;
                        }
                    }
                    else
                    {
                        if (argVal is not null)
                            scalarData[f.Name] = argVal;
                    }
                }

                // Validate scalar data (uniqueness) and relation targets
                await ValidateEntryData(scalarData, relationValues, collectionFields, mutationDb, collectionId, excludeEntryId: null);

                var entrySlug = slug ?? GenerateSlug(name);
                var slugConflict = await mutationDb.Entries
                    .AnyAsync(e => e.CollectionId == collectionId && e.Slug == entrySlug);
                if (slugConflict)
                    entrySlug = $"{entrySlug}-{Guid.NewGuid().ToString("N")[..8]}";

                Locale locale = localeArg ?? collection.DefaultLocale;

                EntryStatus status = statusArg ?? EntryStatus.Draft;

                var now = DateTime.UtcNow;
                var json = JsonSerializer.Serialize(scalarData.Where(kvp => kvp.Value is not null)
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value));

                var entry = new Entry
                {
                    Id = Guid.NewGuid(),
                    Name = name,
                    Slug = entrySlug,
                    CollectionId = collectionId,
                    CreatedBy = userId,
                    Locale = locale,
                    DefaultLocale = collection.DefaultLocale,
                    Status = status,
                    Data = JsonDocument.Parse(json),
                    CreatedAt = now,
                    UpdatedAt = now,
                    PublishedAt = status == EntryStatus.Published ? now : null
                };

                mutationDb.Entries.Add(entry);

                // Write relation rows
                foreach (var (fieldId, targetIdStr) in relationValues)
                {
                    if (Guid.TryParse(targetIdStr, out var targetId))
                    {
                        mutationDb.EntryRelations.Add(new EntryRelation
                        {
                            EntryId = entry.Id,
                            FieldId = fieldId,
                            TargetEntryId = targetId
                        });
                    }
                }

                await mutationDb.SaveChangesAsync();

                return entry;
            };

            collectionMutationsTypeDef.Fields.Add(createFieldDef);

            // ── update mutation ──
            var updateFieldDef = new ObjectFieldDefinition(
                "update",
                $"Update an existing entry in the '{collection.Name}' collection",
                TypeReference.Parse($"{entryTypeName}!"));

            updateFieldDef.Arguments.Add(new ArgumentDefinition(
                "id", "Entry ID to update", TypeReference.Parse("ID!")));

            updateFieldDef.Arguments.Add(new ArgumentDefinition(
                "name", "New entry name", TypeReference.Parse("String")));

            updateFieldDef.Arguments.Add(new ArgumentDefinition(
                "slug", "New URL-friendly identifier", TypeReference.Parse("String")));

            updateFieldDef.Arguments.Add(new ArgumentDefinition(
                "locale", "New locale", TypeReference.Parse("Locale")));

            updateFieldDef.Arguments.Add(new ArgumentDefinition(
                "status", "New status", TypeReference.Parse("EntryStatus")));

            updateFieldDef.Arguments.Add(new ArgumentDefinition(
                "data", $"Dynamic data for the '{collection.Name}' collection (partial update)",
                TypeReference.Parse(updateDataInputTypeName)));

            updateFieldDef.Resolver = async ctx =>
            {
                var mutationDb = ctx.Service<TechtonicCmsDbContext>();
                var abacService = ctx.Service<AbacService>();
                var httpContextAccessor = ctx.Service<IHttpContextAccessor>();

                var userId = GetUserId(httpContextAccessor);
                await abacService.RequirePermissionAsync(userId, BaseResource.Entries, PermissionAction.Update);

                var idStr = ctx.ArgumentValue<string>("id");
                if (!Guid.TryParse(idStr, out var entryId))
                    throw new GraphQLException(ErrorBuilder.New()
                        .SetMessage("Invalid entry ID")
                        .SetCode("BAD_REQUEST")
                        .Build());

                var entry = await mutationDb.Entries.FirstOrDefaultAsync(e =>
                    e.Id == entryId && e.CollectionId == collectionId);

                if (entry is null)
                    throw new GraphQLException(ErrorBuilder.New()
                        .SetMessage("Entry not found")
                        .SetCode("NOT_FOUND")
                        .Build());

                var name = ctx.ArgumentValue<string>("name");
                var slug = ctx.ArgumentValue<string>("slug");
                var localeArg = ctx.ArgumentValue<Locale?>("locale");
                var statusArg = ctx.ArgumentValue<EntryStatus?>("status");
                var data = ctx.ArgumentValue<Dictionary<string, object?>?>("data")
                    ?? new Dictionary<string, object?>();

                if (name is not null) entry.Name = name;
                if (slug is not null)
                {
                    var slugConflict = await mutationDb.Entries
                        .AnyAsync(e => e.CollectionId == collectionId && e.Slug == slug && e.Id != entryId);
                    if (slugConflict)
                        throw new GraphQLException(ErrorBuilder.New()
                            .SetMessage($"An entry with slug '{slug}' already exists in this collection")
                            .SetCode("CONFLICT")
                            .Build());
                    entry.Slug = slug;
                }
                if (localeArg.HasValue)
                    entry.Locale = localeArg.Value;
                if (statusArg.HasValue)
                {
                    entry.Status = statusArg.Value;
                    if (statusArg.Value == EntryStatus.Published && entry.PublishedAt is null)
                        entry.PublishedAt = DateTime.UtcNow;
                }

                // Merge dynamic field data from the nested data input
                var collectionFields = await mutationDb.Fields
                    .Where(f => f.CollectionId == collectionId)
                    .ToListAsync();

                if (data.Count > 0)
                {
                    // Field-level ABAC: determine which fields the user can write to
                    var writableFields = await abacService.FilterAccessibleFieldsAsync(
                        userId, collectionFields, PermissionAction.Update);
                    var writableFieldNames = writableFields.Select(f => f.Name).ToHashSet();
                    var writableFieldIds = writableFields.Select(f => f.Id).ToHashSet();

                    var existingData = JsonSerializer.Deserialize<Dictionary<string, object?>>(
                        entry.Data.RootElement.GetRawText(),
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                        ?? new Dictionary<string, object?>();

                    var scalarChanged = false;
                    var relationChanges = new Dictionary<Guid, string?>(); // fieldId → targetId (null = remove)

                    foreach (var f in collectionFields)
                    {
                        if (!data.TryGetValue(f.Name, out var argVal))
                            continue;

                        // Check field-level write permission
                        if (f.DataType == FieldDataType.Relation)
                        {
                            if (!writableFieldIds.Contains(f.Id))
                            {
                                throw new GraphQLException(ErrorBuilder.New()
                                    .SetMessage($"Permission denied: cannot write to field '{f.Name}'")
                                    .SetCode("FORBIDDEN")
                                    .Build());
                            }
                            // Track relation changes for the join table
                            relationChanges[f.Id] = argVal?.ToString();
                        }
                        else
                        {
                            if (!writableFieldNames.Contains(f.Name))
                            {
                                throw new GraphQLException(ErrorBuilder.New()
                                    .SetMessage($"Permission denied: cannot write to field '{f.Name}'")
                                    .SetCode("FORBIDDEN")
                                    .Build());
                            }
                            if (argVal is not null)
                            {
                                existingData[f.Name] = argVal;
                                scalarChanged = true;
                            }
                        }
                    }

                    if (scalarChanged)
                    {
                        // Validate unique constraints on scalar data only
                        await ValidateEntryData(existingData, new Dictionary<Guid, string>(), collectionFields, mutationDb, collectionId, excludeEntryId: entryId);
                        entry.Data.Dispose();
                        entry.Data = JsonDocument.Parse(
                            JsonSerializer.Serialize(existingData.Where(kvp => kvp.Value is not null)
                                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value)));
                    }

                    // Apply relation changes to join table
                    if (relationChanges.Count > 0)
                    {
                        var changedFieldIds = relationChanges.Keys.ToList();
                        var existingRelations = await mutationDb.EntryRelations
                            .Where(er => er.EntryId == entryId && changedFieldIds.Contains(er.FieldId))
                            .ToListAsync();

                        // Validate relation targets
                        var relationValues = relationChanges
                            .Where(kvp => kvp.Value is not null)
                            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value!);
                        await ValidateEntryData(new Dictionary<string, object?>(), relationValues, collectionFields, mutationDb, collectionId, excludeEntryId: entryId);

                        foreach (var (fieldId, targetIdStr) in relationChanges)
                        {
                            var existing = existingRelations.FirstOrDefault(er => er.FieldId == fieldId);

                            if (targetIdStr is null)
                            {
                                // Remove relation
                                if (existing is not null)
                                    mutationDb.EntryRelations.Remove(existing);
                            }
                            else if (Guid.TryParse(targetIdStr, out var targetId))
                            {
                                if (existing is not null)
                                {
                                    // Update existing relation
                                    existing.TargetEntryId = targetId;
                                }
                                else
                                {
                                    // Add new relation
                                    mutationDb.EntryRelations.Add(new EntryRelation
                                    {
                                        EntryId = entryId,
                                        FieldId = fieldId,
                                        TargetEntryId = targetId
                                    });
                                }
                            }
                        }
                    }
                }

                entry.UpdatedAt = DateTime.UtcNow;
                await mutationDb.SaveChangesAsync();

                return entry;
            };

            collectionMutationsTypeDef.Fields.Add(updateFieldDef);

            // ── delete mutation ──
            var deleteFieldDef = new ObjectFieldDefinition(
                "delete",
                $"Soft-delete an entry in the '{collection.Name}' collection",
                TypeReference.Parse($"{entryTypeName}!"));

            deleteFieldDef.Arguments.Add(new ArgumentDefinition(
                "id", "Entry ID to delete", TypeReference.Parse("ID!")));

            deleteFieldDef.Resolver = async ctx =>
            {
                var mutationDb = ctx.Service<TechtonicCmsDbContext>();
                var abacService = ctx.Service<AbacService>();
                var httpContextAccessor = ctx.Service<IHttpContextAccessor>();

                var userId = GetUserId(httpContextAccessor);
                await abacService.RequirePermissionAsync(userId, BaseResource.Entries, PermissionAction.Delete);

                var idStr = ctx.ArgumentValue<string>("id");
                if (!Guid.TryParse(idStr, out var entryId))
                    throw new GraphQLException(ErrorBuilder.New()
                        .SetMessage("Invalid entry ID")
                        .SetCode("BAD_REQUEST")
                        .Build());

                var entry = await mutationDb.Entries.FirstOrDefaultAsync(e =>
                    e.Id == entryId && e.CollectionId == collectionId);

                if (entry is null)
                    throw new GraphQLException(ErrorBuilder.New()
                        .SetMessage("Entry not found")
                        .SetCode("NOT_FOUND")
                        .Build());

                entry.Status = EntryStatus.Deleted;
                entry.UpdatedAt = DateTime.UtcNow;
                await mutationDb.SaveChangesAsync();

                return entry;
            };

            collectionMutationsTypeDef.Fields.Add(deleteFieldDef);

            // ── publish mutation ──
            var publishFieldDef = new ObjectFieldDefinition(
                "publish",
                $"Publish an entry in the '{collection.Name}' collection",
                TypeReference.Parse($"{entryTypeName}!"));

            publishFieldDef.Arguments.Add(new ArgumentDefinition(
                "id", "Entry ID to publish", TypeReference.Parse("ID!")));

            publishFieldDef.Resolver = async ctx =>
            {
                var mutationDb = ctx.Service<TechtonicCmsDbContext>();
                var abacService = ctx.Service<AbacService>();
                var httpContextAccessor = ctx.Service<IHttpContextAccessor>();

                var userId = GetUserId(httpContextAccessor);
                await abacService.RequirePermissionAsync(userId, BaseResource.Entries, PermissionAction.Publish);

                var idStr = ctx.ArgumentValue<string>("id");
                if (!Guid.TryParse(idStr, out var entryId))
                    throw new GraphQLException(ErrorBuilder.New()
                        .SetMessage("Invalid entry ID")
                        .SetCode("BAD_REQUEST")
                        .Build());

                var entry = await mutationDb.Entries.FirstOrDefaultAsync(e =>
                    e.Id == entryId && e.CollectionId == collectionId);

                if (entry is null)
                    throw new GraphQLException(ErrorBuilder.New()
                        .SetMessage("Entry not found")
                        .SetCode("NOT_FOUND")
                        .Build());

                entry.Status = EntryStatus.Published;
                entry.PublishedAt = DateTime.UtcNow;
                entry.UpdatedAt = DateTime.UtcNow;
                await mutationDb.SaveChangesAsync();

                return entry;
            };

            collectionMutationsTypeDef.Fields.Add(publishFieldDef);

            types.Add(ObjectType.CreateUnsafe(collectionMutationsTypeDef));

            // Add field on EntriesMutations pointing to this collection's mutation type
            entriesMutationsTypeDef.Fields.Add(new ObjectFieldDefinition(
                camelName,
                $"Mutations for the '{collection.Name}' collection",
                TypeReference.Parse($"{collectionMutationsTypeName}!"),
                pureResolver: _ => new Dictionary<string, object>()));
        }

        // Wire dynamicCollections field on Mutation root
        var mutationExtensionDef = new ObjectTypeDefinition("CollectionMutation")
        {
            IsExtension = true,
            RuntimeType = typeof(CollectionMutation)
        };

        mutationExtensionDef.Fields.Add(new ObjectFieldDefinition(
            "entries",
            "Mutations for all entries",
            TypeReference.Parse("EntriesMutations!"),
            pureResolver: _ => new Dictionary<string, object>()));

        types.Add(ObjectTypeExtension.CreateUnsafe(mutationExtensionDef));

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
    /// Adds a dynamic field to the filter descriptor based on the field's <see cref="FieldDataType"/>.
    /// Scalar fields use <see cref="CmsDbFunctions"/> JSONB extraction; relation fields use
    /// <see cref="Entry.FromRelations"/> navigation property subqueries that EF Core translates
    /// to <c>EXISTS</c> / correlated subselect on <c>entry_relations</c>.
    /// </summary>
    private static void AddDynamicFilterField(IFilterInputTypeDescriptor<Entry> filterDesc, Field field)
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

            // Relation fields: filter by target entry ID via entry_relations subquery.
            // EF Core translates this to: WHERE (SELECT r."TargetEntryId" FROM entry_relations r
            //   WHERE r."EntryId" = e."Id" AND r."FieldId" = @fieldId LIMIT 1) = @value
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
    /// Scalar fields use <see cref="CmsDbFunctions"/> JSONB extraction; relation fields use
    /// <see cref="Entry.FromRelations"/> navigation property subqueries to sort by target entry name.
    /// </summary>
    private static void AddDynamicSortField(ISortInputTypeDescriptor<Entry> sortDesc, Field field)
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

            // Relation fields: sort by target entry name via entry_relations subquery.
            // EF Core translates: ORDER BY (SELECT r."TargetEntry"."Name" FROM entry_relations r
            //   JOIN entries AS "TargetEntry" ON r."TargetEntryId" = "TargetEntry"."Id"
            //   WHERE r."EntryId" = e."Id" AND r."FieldId" = @fieldId LIMIT 1)
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

    private static Guid GetUserId(IHttpContextAccessor httpContextAccessor)
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

    private static async Task ValidateEntryData(
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

    private static string GenerateSlug(string name)
    {
        var slug = name.Trim().ToLowerInvariant();
        slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[^a-z0-9]+", "-");
        slug = System.Text.RegularExpressions.Regex.Replace(slug, @"^-+|-+$", "");
        slug = slug.Trim('-');

        if (string.IsNullOrWhiteSpace(slug))
            slug = Guid.NewGuid().ToString("N")[..8];

        return slug;
    }
}
