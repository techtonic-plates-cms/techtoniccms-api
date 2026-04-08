using HotChocolate;
using HotChocolate.Authorization;
using HotChocolate.Types;

using Microsoft.EntityFrameworkCore;
using TechtonicCmsApi.Contexts;
using TechtonicCmsApi.Schema.TechtonicCms.Entities;
using TechtonicCmsApi.Schema.TechtonicCms.Enums;
using TechtonicCmsApi.Services;
using TechtonicCmsApi.Types.Collections.DynamicCollections;

namespace TechtonicCmsApi.Types.Collections;

public class FieldDefinitionInput
{
    [GraphQLType<NonNullType<StringType>>]
    public string Name { get; set; } = "";

    public string? Label { get; set; }

    public string? Description { get; set; }

    [GraphQLType<NonNullType<StringType>>]
    public string DataType { get; set; } = "";

    public bool? IsRequired { get; set; }

    public bool? IsUnique { get; set; }

    public bool? IsPublic { get; set; }

    public bool? IsPii { get; set; }

    public bool? IsEncrypted { get; set; }

    public string? SensitivityLevel { get; set; }

    public string? ValidationRules { get; set; }

    public string? DefaultValue { get; set; }

    public string? HelpText { get; set; }
}

public class FieldUpdateDefinitionInput
{
    public Guid? Id { get; set; }

    [GraphQLType<NonNullType<StringType>>]
    public string Name { get; set; } = "";

    public string? Label { get; set; }

    public string? Description { get; set; }

    public string? DataType { get; set; }

    public bool? IsRequired { get; set; }

    public bool? IsUnique { get; set; }

    public bool? IsPublic { get; set; }

    public bool? IsPii { get; set; }

    public bool? IsEncrypted { get; set; }

    public string? SensitivityLevel { get; set; }

    public string? ValidationRules { get; set; }

    public string? DefaultValue { get; set; }

    public string? HelpText { get; set; }
}

public class CreateCollectionInput
{
    [GraphQLType<NonNullType<StringType>>]
    public string Name { get; set; } = "";

    [GraphQLType<NonNullType<StringType>>]
    public string Slug { get; set; } = "";

    public string? Description { get; set; }

    public string? Icon { get; set; }

    public string? Color { get; set; }

    public string? DefaultLocale { get; set; }

    public string[]? SupportedLocales { get; set; }

    public bool? IsLocalized { get; set; }

    public FieldDefinitionInput[]? Fields { get; set; }
}

public class UpdateCollectionInput
{
    [GraphQLType<NonNullType<IdType>>]
    public Guid Id { get; set; }

    public string? Name { get; set; }

    public string? Slug { get; set; }

    public string? Description { get; set; }

    public string? Icon { get; set; }

    public string? Color { get; set; }

    public string? DefaultLocale { get; set; }

    public string[]? SupportedLocales { get; set; }

    public bool? IsLocalized { get; set; }

    public FieldUpdateDefinitionInput[]? Fields { get; set; }

    public Guid[]? DeleteFieldIds { get; set; }
}

public class CollectionMutation
{
    [Authorize(Policy = "Collections:Create")]
    public async Task<Collection> Create(
        CreateCollectionInput input,
        [Service] TechtonicCmsDbContext db,
        [Service] AbacService abacService,
        [Service] IHttpContextAccessor httpContextAccessor,
        [Service] CollectionTypeModule typeModule)
    {
        var userId = GetUserId(httpContextAccessor);
        await abacService.RequirePermissionAsync(userId, BaseResource.Collections, PermissionAction.Create);

        var slugExists = await db.Collections.AnyAsync(c => c.Slug == input.Slug);
        if (slugExists)
            throw new GraphQLException(ErrorBuilder.New()
                .SetMessage($"A collection with slug '{input.Slug}' already exists")
                .SetCode("CONFLICT")
                .Build());

        Locale defaultLocale = Locale.En;
        if (!string.IsNullOrEmpty(input.DefaultLocale) &&
            Enum.TryParse<Locale>(input.DefaultLocale, true, out var parsedLocale))
            defaultLocale = parsedLocale;

        var supportedLocales = input.SupportedLocales ?? ["en"];
        var now = DateTime.UtcNow;

        var collection = new Collection
        {
            Id = Guid.NewGuid(),
            Name = input.Name,
            Slug = input.Slug,
            Description = input.Description,
            Icon = input.Icon,
            Color = input.Color,
            DefaultLocale = defaultLocale,
            SupportedLocales = supportedLocales,
            IsLocalized = input.IsLocalized ?? false,
            CreatedBy = userId,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.Collections.Add(collection);
        await db.SaveChangesAsync();

        if (input.Fields is { Length: > 0 })
        {
            var fieldNames = new HashSet<string>();
            foreach (var fieldInput in input.Fields)
            {
                if (!fieldNames.Add(fieldInput.Name))
                    throw new GraphQLException(ErrorBuilder.New()
                        .SetMessage($"Duplicate field name '{fieldInput.Name}' in collection definition")
                        .SetCode("BAD_REQUEST")
                        .Build());

                if (!Enum.TryParse<FieldDataType>(fieldInput.DataType, true, out var dataType))
                    throw new GraphQLException(ErrorBuilder.New()
                        .SetMessage($"Invalid field data type: {fieldInput.DataType}")
                        .SetCode("INVALID_ENUM")
                        .Build());

                db.Fields.Add(new Field
                {
                    Id = Guid.NewGuid(),
                    CollectionId = collection.Id,
                    Name = fieldInput.Name,
                    Label = fieldInput.Label,
                    Description = fieldInput.Description,
                    DataType = dataType,
                    IsRequired = fieldInput.IsRequired ?? false,
                    IsUnique = fieldInput.IsUnique ?? false,
                    IsPublic = fieldInput.IsPublic ?? true,
                    IsPii = fieldInput.IsPii ?? false,
                    IsEncrypted = fieldInput.IsEncrypted ?? false,
                    SensitivityLevel = fieldInput.SensitivityLevel ?? "PUBLIC",
                    ValidationRules = fieldInput.ValidationRules,
                    DefaultValue = fieldInput.DefaultValue,
                    HelpText = fieldInput.HelpText,
                    CreatedBy = userId,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }

            await db.SaveChangesAsync();
        }

        typeModule.TriggerTypesChanged();

        return collection;
    }

    [Authorize(Policy = "Collections:Update")]
    public async Task<Collection> Update(
        UpdateCollectionInput input,
        [Service] TechtonicCmsDbContext db,
        [Service] AbacService abacService,
        [Service] IHttpContextAccessor httpContextAccessor,
        [Service] CollectionTypeModule typeModule)
    {
        var userId = GetUserId(httpContextAccessor);
        await abacService.RequirePermissionAsync(userId, BaseResource.Collections, PermissionAction.Update);

        if (input.Fields is { Length: > 0 } || input.DeleteFieldIds is { Length: > 0 })
            await abacService.RequirePermissionAsync(userId, BaseResource.Collections, PermissionAction.ManageSchema);

        var collection = await db.Collections.FindAsync(input.Id);
        if (collection is null)
            throw new GraphQLException(ErrorBuilder.New()
                .SetMessage("Collection not found")
                .SetCode("NOT_FOUND")
                .Build());

        if (input.Name is not null)
            collection.Name = input.Name;

        if (input.Slug is not null)
        {
            var slugExists = await db.Collections.AnyAsync(c => c.Slug == input.Slug && c.Id != input.Id);
            if (slugExists)
                throw new GraphQLException(ErrorBuilder.New()
                    .SetMessage($"A collection with slug '{input.Slug}' already exists")
                    .SetCode("CONFLICT")
                    .Build());

            collection.Slug = input.Slug;
        }

        if (input.Description is not null)
            collection.Description = input.Description;

        if (input.Icon is not null)
            collection.Icon = input.Icon;

        if (input.Color is not null)
            collection.Color = input.Color;

        if (input.DefaultLocale is not null &&
            Enum.TryParse<Locale>(input.DefaultLocale, true, out var parsedLocale))
            collection.DefaultLocale = parsedLocale;

        if (input.SupportedLocales is not null)
            collection.SupportedLocales = input.SupportedLocales;

        if (input.IsLocalized.HasValue)
            collection.IsLocalized = input.IsLocalized.Value;

        var now = DateTime.UtcNow;

        if (input.DeleteFieldIds is { Length: > 0 })
        {
            var fieldsToDelete = await db.Fields
                .Where(f => input.DeleteFieldIds.Contains(f.Id) && f.CollectionId == collection.Id)
                .ToListAsync();

            db.Fields.RemoveRange(fieldsToDelete);
        }

        if (input.Fields is { Length: > 0 })
        {
            var existingFields = await db.Fields
                .Where(f => f.CollectionId == collection.Id)
                .ToDictionaryAsync(f => f.Id);

            foreach (var fieldInput in input.Fields)
            {
                if (fieldInput.Id.HasValue && existingFields.TryGetValue(fieldInput.Id.Value, out var existing))
                {
                    var nameConflict = await db.Fields.AnyAsync(f =>
                        f.CollectionId == collection.Id &&
                        f.Name == fieldInput.Name &&
                        f.Id != existing.Id);

                    if (nameConflict)
                        throw new GraphQLException(ErrorBuilder.New()
                            .SetMessage($"A field with name '{fieldInput.Name}' already exists in this collection")
                            .SetCode("CONFLICT")
                            .Build());

                    existing.Name = fieldInput.Name;
                    existing.Label = fieldInput.Label;
                    existing.Description = fieldInput.Description;

                    if (fieldInput.DataType is not null)
                    {
                        if (!Enum.TryParse<FieldDataType>(fieldInput.DataType, true, out var dataType))
                            throw new GraphQLException(ErrorBuilder.New()
                                .SetMessage($"Invalid field data type: {fieldInput.DataType}")
                                .SetCode("INVALID_ENUM")
                                .Build());

                        existing.DataType = dataType;
                    }

                    if (fieldInput.IsRequired.HasValue) existing.IsRequired = fieldInput.IsRequired.Value;
                    if (fieldInput.IsUnique.HasValue) existing.IsUnique = fieldInput.IsUnique.Value;
                    if (fieldInput.IsPublic.HasValue) existing.IsPublic = fieldInput.IsPublic.Value;
                    if (fieldInput.IsPii.HasValue) existing.IsPii = fieldInput.IsPii.Value;
                    if (fieldInput.IsEncrypted.HasValue) existing.IsEncrypted = fieldInput.IsEncrypted.Value;
                    if (fieldInput.SensitivityLevel is not null) existing.SensitivityLevel = fieldInput.SensitivityLevel;
                    if (fieldInput.ValidationRules is not null) existing.ValidationRules = fieldInput.ValidationRules;
                    if (fieldInput.DefaultValue is not null) existing.DefaultValue = fieldInput.DefaultValue;
                    if (fieldInput.HelpText is not null) existing.HelpText = fieldInput.HelpText;

                    existing.UpdatedAt = now;
                }
                else
                {
                    if (!Enum.TryParse<FieldDataType>(fieldInput.DataType, true, out var dataType))
                        throw new GraphQLException(ErrorBuilder.New()
                            .SetMessage($"Invalid field data type: {fieldInput.DataType}")
                            .SetCode("INVALID_ENUM")
                            .Build());

                    var nameConflict = await db.Fields.AnyAsync(f =>
                        f.CollectionId == collection.Id && f.Name == fieldInput.Name);

                    if (nameConflict)
                        throw new GraphQLException(ErrorBuilder.New()
                            .SetMessage($"A field with name '{fieldInput.Name}' already exists in this collection")
                            .SetCode("CONFLICT")
                            .Build());

                    db.Fields.Add(new Field
                    {
                        Id = Guid.NewGuid(),
                        CollectionId = collection.Id,
                        Name = fieldInput.Name,
                        Label = fieldInput.Label,
                        Description = fieldInput.Description,
                        DataType = dataType,
                        IsRequired = fieldInput.IsRequired ?? false,
                        IsUnique = fieldInput.IsUnique ?? false,
                        IsPublic = fieldInput.IsPublic ?? true,
                        IsPii = fieldInput.IsPii ?? false,
                        IsEncrypted = fieldInput.IsEncrypted ?? false,
                        SensitivityLevel = fieldInput.SensitivityLevel ?? "PUBLIC",
                        ValidationRules = fieldInput.ValidationRules,
                        DefaultValue = fieldInput.DefaultValue,
                        HelpText = fieldInput.HelpText,
                        CreatedBy = userId,
                        CreatedAt = now,
                        UpdatedAt = now
                    });
                }
            }
        }

        collection.UpdatedAt = now;
        await db.SaveChangesAsync();

        typeModule.TriggerTypesChanged();

        return collection;
    }

    [Authorize(Policy = "Collections:Delete")]
    public async Task<bool> Delete(
        Guid id,
        [Service] TechtonicCmsDbContext db,
        [Service] AbacService abacService,
        [Service] IHttpContextAccessor httpContextAccessor,
        [Service] CollectionTypeModule typeModule)
    {
        var userId = GetUserId(httpContextAccessor);
        await abacService.RequirePermissionAsync(userId, BaseResource.Collections, PermissionAction.Delete);

        var collection = await db.Collections.FindAsync(id);
        if (collection is null)
            throw new GraphQLException(ErrorBuilder.New()
                .SetMessage("Collection not found")
                .SetCode("NOT_FOUND")
                .Build());

        var hasEntries = await db.Entries.AnyAsync(e => e.CollectionId == id);
        if (hasEntries)
            throw new GraphQLException(ErrorBuilder.New()
                .SetMessage("Cannot delete collection with existing entries. Delete entries first.")
                .SetCode("CONFLICT")
                .Build());

        db.Collections.Remove(collection);
        await db.SaveChangesAsync();

        typeModule.TriggerTypesChanged();

        return true;
    }

    private static Guid GetUserId(IHttpContextAccessor httpContextAccessor)
    {
        var userIdStr = httpContextAccessor.HttpContext?.User.FindFirst("userId")?.Value;
        if (userIdStr is null || !Guid.TryParse(userIdStr, out var userId))
            throw new GraphQLException(ErrorBuilder.New()
                .SetMessage("Authentication required")
                .SetCode("UNAUTHENTICATED")
                .Build());

        return userId;
    }
}

[ExtendObjectType(nameof(Mutation))]
public static class CollectionMutations
{
    public static CollectionMutation Collections() => new();
}
