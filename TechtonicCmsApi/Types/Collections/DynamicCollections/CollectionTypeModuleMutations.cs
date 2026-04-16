using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using HotChocolate;
using HotChocolate.Resolvers;
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
/// Mutation-related type generation for <see cref="CollectionTypeModule"/>.
/// Builds dynamic per-collection entry mutations (create, update, delete, publish).
/// </summary>
public partial class CollectionTypeModule
{
    /// <summary>
    /// Builds all mutation-related GraphQL types: the <c>EntriesMutations</c> root type,
    /// per-collection mutation sub-types with create/update/delete/publish operations,
    /// dynamic input types, and the <c>CollectionMutation</c> extension.
    /// </summary>
    private void BuildMutationTypes(
        List<Collection> collections,
        List<ITypeSystemMember> types)
    {
        // ── EntriesMutations root type ──
        var entriesMutationsTypeDef = new ObjectTypeDefinition("EntriesMutations")
        {
            Description = "Root type for all entry mutations",
            RuntimeType = typeof(Dictionary<string, object>)
        };

        types.Add(ObjectType.CreateUnsafe(entriesMutationsTypeDef));

        // ── Per-collection mutation types ──
        foreach (var collection in collections)
        {
            var pascalName = DynamicCollectionHelpers.ToPascalCase(collection.Slug);
            var camelName = DynamicCollectionHelpers.ToCamelCase(collection.Slug);
            var collectionMutationsTypeName = $"{pascalName}Mutations";
            var entryTypeName = $"{pascalName}Entry";
            var collectionId = collection.Id;

            // ── Per-collection mutation sub-type ──
            var collectionMutationsTypeDef = new ObjectTypeDefinition(collectionMutationsTypeName)
            {
                Description = $"Mutations for the '{collection.Name}' collection",
                RuntimeType = typeof(Dictionary<string, object>)
            };

            // ── Dynamic input types ──
            var (createDataInputDef, updateDataInputDef) =
                DynamicCollectionHelpers.BuildInputTypeDefinitions(collection);

            types.Add(InputObjectType.CreateUnsafe(createDataInputDef));
            types.Add(InputObjectType.CreateUnsafe(updateDataInputDef));

            // ── Mutation resolvers ──
            collectionMutationsTypeDef.Fields.Add(
                BuildCreateMutation(collection, createDataInputDef.Name, entryTypeName));

            collectionMutationsTypeDef.Fields.Add(
                BuildUpdateMutation(collection, updateDataInputDef.Name, entryTypeName));

            collectionMutationsTypeDef.Fields.Add(
                BuildDeleteMutation(collection, entryTypeName));

            collectionMutationsTypeDef.Fields.Add(
                BuildPublishMutation(collection, entryTypeName));

            types.Add(ObjectType.CreateUnsafe(collectionMutationsTypeDef));

            // Wire this collection's mutations into EntriesMutations root
            entriesMutationsTypeDef.Fields.Add(new ObjectFieldDefinition(
                camelName,
                $"Mutations for the '{collection.Name}' collection",
                TypeReference.Parse($"{collectionMutationsTypeName}!"),
                pureResolver: _ => new Dictionary<string, object>()));
        }

        // ── Wire entries field onto CollectionMutation root ──
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
    }

    // ──────────────────────────────────────────────────────────────────
    // Individual Mutation Builders
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds the <c>create</c> mutation field definition for a collection.
    /// Separates scalar (JSONB) vs relation field values, validates, and writes both.
    /// </summary>
    private static ObjectFieldDefinition BuildCreateMutation(
        Collection collection,
        string createDataInputTypeName,
        string entryTypeName)
    {
        var collectionId = collection.Id;

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

            var userId = DynamicCollectionHelpers.GetUserId(httpContextAccessor);
            await abacService.RequirePermissionAsync(userId, BaseResource.Entries, PermissionAction.Create, new Dictionary<string, object?>
            {
                ["ResourceEntryCollectionId"] = collectionId.ToString()
            });

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

            // Separate scalar vs relation field values from the nested data input
            var (scalarData, relationValues) = SeparateFieldValues(data, collectionFields);

            // Validate required fields
            ValidateRequiredFields(data, collectionFields);

            // Validate scalar data (uniqueness) and relation targets
            await DynamicCollectionHelpers.ValidateEntryData(scalarData, relationValues, collectionFields, mutationDb, collectionId, excludeEntryId: null);

            var entrySlug = slug ?? DynamicCollectionHelpers.GenerateSlug(name);
            var slugConflict = await mutationDb.Entries
                .AnyAsync(e => e.CollectionId == collectionId && e.Slug == entrySlug);
            if (slugConflict)
                entrySlug = $"{entrySlug}-{Guid.NewGuid().ToString("N")[..8]}";

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
                Locale = localeArg ?? collection.DefaultLocale,
                DefaultLocale = collection.DefaultLocale,
                Status = statusArg ?? EntryStatus.Draft,
                Data = JsonDocument.Parse(json),
                CreatedAt = now,
                UpdatedAt = now,
                PublishedAt = statusArg == EntryStatus.Published ? now : null
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

        return createFieldDef;
    }

    /// <summary>
    /// Builds the <c>update</c> mutation field definition for a collection.
    /// Merges dynamic field data, handles relation changes, and validates.
    /// </summary>
    private static ObjectFieldDefinition BuildUpdateMutation(
        Collection collection,
        string updateDataInputTypeName,
        string entryTypeName)
    {
        var collectionId = collection.Id;

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

            var userId = DynamicCollectionHelpers.GetUserId(httpContextAccessor);

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

            await abacService.RequirePermissionAsync(userId, BaseResource.Entries, PermissionAction.Update, new Dictionary<string, object?>
            {
                ["ResourceEntryId"] = entry.Id.ToString(),
                ["ResourceEntryStatus"] = entry.Status.ToString(),
                ["ResourceEntryCreatedBy"] = entry.CreatedBy.ToString(),
                ["ResourceEntryCollectionId"] = entry.CollectionId.ToString(),
                ["ResourceEntryLocale"] = entry.Locale.ToString(),
                ["ResourceEntryPublishedAt"] = entry.PublishedAt?.ToString("O"),
            });

            var data = ctx.ArgumentValue<Dictionary<string, object?>?>("data")
                ?? new Dictionary<string, object?>();

            // Apply static field updates — only when the argument was explicitly sent.
            // ArgumentOptional<T>.HasValue is false when the argument is absent from the request,
            // ensuring omitted fields are never overwritten.
            var nameOpt = ctx.ArgumentOptional<string?>("name");
            if (nameOpt.HasValue)
                entry.Name = nameOpt.Value;

            var slugOpt = ctx.ArgumentOptional<string?>("slug");
            if (slugOpt.HasValue && slugOpt.Value is not null)
            {
                var slugConflict = await mutationDb.Entries
                    .AnyAsync(e => e.CollectionId == collectionId && e.Slug == slugOpt.Value && e.Id != entryId);
                if (slugConflict)
                    throw new GraphQLException(ErrorBuilder.New()
                        .SetMessage($"An entry with slug '{slugOpt.Value}' already exists in this collection")
                        .SetCode("CONFLICT")
                        .Build());
                entry.Slug = slugOpt.Value;
            }

            var localeOpt = ctx.ArgumentOptional<Locale?>("locale");
            if (localeOpt.HasValue && localeOpt.Value.HasValue)
                entry.Locale = localeOpt.Value.Value;

            var statusOpt = ctx.ArgumentOptional<EntryStatus?>("status");
            if (statusOpt.HasValue && statusOpt.Value.HasValue)
            {
                entry.Status = statusOpt.Value.Value;
                if (statusOpt.Value.Value == EntryStatus.Published && entry.PublishedAt is null)
                    entry.PublishedAt = DateTime.UtcNow;
            }

            // Merge dynamic field data from the nested data input
            if (data.Count > 0)
            {
                var collectionFields = await mutationDb.Fields
                    .Where(f => f.CollectionId == collectionId)
                    .ToListAsync();

                await ApplyDynamicFieldUpdates(entry, data, collectionFields, mutationDb, collectionId);
            }

            entry.UpdatedAt = DateTime.UtcNow;
            await mutationDb.SaveChangesAsync();

            return entry;
        };

        return updateFieldDef;
    }

    /// <summary>
    /// Builds the <c>delete</c> mutation field definition for a collection.
    /// Performs a soft-delete by setting status to <see cref="EntryStatus.Deleted"/>.
    /// </summary>
    private static ObjectFieldDefinition BuildDeleteMutation(
        Collection collection,
        string entryTypeName)
    {
        var collectionId = collection.Id;

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

            var userId = DynamicCollectionHelpers.GetUserId(httpContextAccessor);

            var entry = await ResolveEntryAsync(ctx, mutationDb, collectionId);

            await abacService.RequirePermissionAsync(userId, BaseResource.Entries, PermissionAction.Delete, new Dictionary<string, object?>
            {
                ["ResourceEntryId"] = entry.Id.ToString(),
                ["ResourceEntryStatus"] = entry.Status.ToString(),
                ["ResourceEntryCreatedBy"] = entry.CreatedBy.ToString(),
                ["ResourceEntryCollectionId"] = entry.CollectionId.ToString(),
                ["ResourceEntryLocale"] = entry.Locale.ToString(),
                ["ResourceEntryPublishedAt"] = entry.PublishedAt?.ToString("O"),
            });

            entry.Status = EntryStatus.Deleted;
            entry.UpdatedAt = DateTime.UtcNow;
            await mutationDb.SaveChangesAsync();

            return entry;
        };

        return deleteFieldDef;
    }

    /// <summary>
    /// Builds the <c>publish</c> mutation field definition for a collection.
    /// Sets status to <see cref="EntryStatus.Published"/> and records the timestamp.
    /// </summary>
    private static ObjectFieldDefinition BuildPublishMutation(
        Collection collection,
        string entryTypeName)
    {
        var collectionId = collection.Id;

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

            var userId = DynamicCollectionHelpers.GetUserId(httpContextAccessor);

            var entry = await ResolveEntryAsync(ctx, mutationDb, collectionId);

            await abacService.RequirePermissionAsync(userId, BaseResource.Entries, PermissionAction.Publish, new Dictionary<string, object?>
            {
                ["ResourceEntryId"] = entry.Id.ToString(),
                ["ResourceEntryStatus"] = entry.Status.ToString(),
                ["ResourceEntryCreatedBy"] = entry.CreatedBy.ToString(),
                ["ResourceEntryCollectionId"] = entry.CollectionId.ToString(),
                ["ResourceEntryLocale"] = entry.Locale.ToString(),
                ["ResourceEntryPublishedAt"] = entry.PublishedAt?.ToString("O"),
            });

            entry.Status = EntryStatus.Published;
            entry.PublishedAt = DateTime.UtcNow;
            entry.UpdatedAt = DateTime.UtcNow;
            await mutationDb.SaveChangesAsync();

            return entry;
        };

        return publishFieldDef;
    }

    // ──────────────────────────────────────────────────────────────────
    // Mutation Helper Methods
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Resolves an entry by the <c>id</c> argument, throwing standard errors if not found.
    /// Used by delete and publish mutations.
    /// </summary>
    private static async Task<Entry> ResolveEntryAsync(
        IResolverContext ctx,
        TechtonicCmsDbContext db,
        Guid collectionId)
    {
        var idStr = ctx.ArgumentValue<string>("id");
        if (!Guid.TryParse(idStr, out var entryId))
            throw new GraphQLException(ErrorBuilder.New()
                .SetMessage("Invalid entry ID")
                .SetCode("BAD_REQUEST")
                .Build());

        var entry = await db.Entries.FirstOrDefaultAsync(e =>
            e.Id == entryId && e.CollectionId == collectionId);

        if (entry is null)
            throw new GraphQLException(ErrorBuilder.New()
                .SetMessage("Entry not found")
                .SetCode("NOT_FOUND")
                .Build());

        return entry;
    }

    /// <summary>
    /// Separates raw input data into scalar (JSONB) and relation (join table) field values.
    /// </summary>
    private static (Dictionary<string, object?> scalarData, Dictionary<Guid, string> relationValues)
        SeparateFieldValues(Dictionary<string, object?> data, List<Field> collectionFields)
    {
        var scalarData = new Dictionary<string, object?>();
        var relationValues = new Dictionary<Guid, string>();

        foreach (var f in collectionFields)
        {
            if (!data.TryGetValue(f.Name, out var argVal))
                continue;

            if (f.DataType == FieldDataType.Relation)
            {
                if (argVal is not null)
                    relationValues[f.Id] = argVal.ToString()!;
            }
            else
            {
                if (argVal is not null)
                    scalarData[f.Name] = argVal;
            }
        }

        return (scalarData, relationValues);
    }

    /// <summary>
    /// Validates that all required fields are present in the input data.
    /// </summary>
    private static void ValidateRequiredFields(
        Dictionary<string, object?> data,
        List<Field> collectionFields)
    {
        foreach (var f in collectionFields)
        {
            if (f.IsRequired && !data.ContainsKey(f.Name))
            {
                throw new GraphQLException(ErrorBuilder.New()
                    .SetMessage($"Required field '{f.Name}' is missing")
                    .SetCode("BAD_REQUEST")
                    .Build());
            }
        }
    }

    /// <summary>
    /// Applies dynamic field updates to an existing entry, handling both scalar (JSONB)
    /// and relation (join table) changes.
    /// </summary>
    private static async Task ApplyDynamicFieldUpdates(
        Entry entry,
        Dictionary<string, object?> data,
        List<Field> collectionFields,
        TechtonicCmsDbContext db,
        Guid collectionId)
    {
        var existingData = JsonSerializer.Deserialize<Dictionary<string, object?>>(
                entry.Data.RootElement.GetRawText(),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? new Dictionary<string, object?>();

        var scalarChanged = false;
        var relationChanges = new Dictionary<Guid, string?>();

        // HotChocolate only populates the dictionary with fields the client explicitly sent;
        // absent optional fields are never added with null, so TryGetValue here means "was provided".
        foreach (var f in collectionFields)
        {
            if (!data.TryGetValue(f.Name, out var argVal))
                continue;

            if (f.DataType == FieldDataType.Relation)
            {
                relationChanges[f.Id] = argVal?.ToString();
            }
            else
            {
                if (argVal is not null)
                {
                    existingData[f.Name] = argVal;
                    scalarChanged = true;
                }
            }
        }

        if (scalarChanged)
        {
            await DynamicCollectionHelpers.ValidateEntryData(
                existingData, new Dictionary<Guid, string>(), collectionFields, db, collectionId, excludeEntryId: entry.Id);

            entry.Data.Dispose();
            entry.Data = JsonDocument.Parse(
                JsonSerializer.Serialize(existingData.Where(kvp => kvp.Value is not null)
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value)));
        }

        if (relationChanges.Count > 0)
        {
            await ApplyRelationChanges(entry.Id, relationChanges, collectionFields, db, collectionId);
        }
    }

    /// <summary>
    /// Applies relation changes to the join table: adds, updates, or removes
    /// <see cref="EntryRelation"/> rows.
    /// </summary>
    private static async Task ApplyRelationChanges(
        Guid entryId,
        Dictionary<Guid, string?> relationChanges,
        List<Field> collectionFields,
        TechtonicCmsDbContext db,
        Guid collectionId)
    {
        var changedFieldIds = relationChanges.Keys.ToList();
        var existingRelations = await db.EntryRelations
            .Where(er => er.EntryId == entryId && changedFieldIds.Contains(er.FieldId))
            .ToListAsync();

        // Validate new relation targets
        var relationValues = relationChanges
            .Where(kvp => kvp.Value is not null)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value!);
        await DynamicCollectionHelpers.ValidateEntryData(
            new Dictionary<string, object?>(), relationValues, collectionFields, db, collectionId, excludeEntryId: entryId);

        foreach (var (fieldId, targetIdStr) in relationChanges)
        {
            var existing = existingRelations.FirstOrDefault(er => er.FieldId == fieldId);

            if (targetIdStr is null)
            {
                if (existing is not null)
                    db.EntryRelations.Remove(existing);
            }
            else if (Guid.TryParse(targetIdStr, out var targetId))
            {
                if (existing is not null)
                {
                    existing.TargetEntryId = targetId;
                }
                else
                {
                    db.EntryRelations.Add(new EntryRelation
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
