using HotChocolate;
using HotChocolate.Types;

using Microsoft.EntityFrameworkCore;
using TechtonicCmsApi.Contexts;
using TechtonicCmsApi.Schema.TechtonicCms.Entities;
using TechtonicCmsApi.Schema.TechtonicCms.Enums;

namespace TechtonicCmsApi.Types.Fields;

[ObjectType<Field>]
public static partial class FieldType
{
    public static string GetId([Parent] Field field) => field.Id.ToString();

    public static string GetCollectionId([Parent] Field field) => field.CollectionId.ToString();

    [GraphQLType<NonNullType<StringType>>]
    public static string GetName([Parent] Field field) => field.Name;

    public static string? GetLabel([Parent] Field field) => field.Label;

    public static string? GetDescription([Parent] Field field) => field.Description;

    public static FieldDataType GetDataType([Parent] Field field) => field.DataType;

    public static bool GetIsRequired([Parent] Field field) => field.IsRequired;

    public static bool GetIsUnique([Parent] Field field) => field.IsUnique;

    public static string? GetValidationRules([Parent] Field field) => field.ValidationRules;

    public static string? GetDefaultValue([Parent] Field field) => field.DefaultValue;

    public static string? GetHelpText([Parent] Field field) => field.HelpText;

    public static string? GetRelatedCollectionId([Parent] Field field) =>
        field.RelatedCollectionId?.ToString();

    public static async Task<Collection?> GetRelatedCollection(
        [Parent] Field field,
        [Service] TechtonicCmsDbContext db)
    {
        if (field.RelatedCollectionId is null) return null;
        return await db.Collections.FindAsync(field.RelatedCollectionId.Value);
    }

    public static string? GetCreatedAt([Parent] Field field) =>
        field.CreatedAt.ToUniversalTime().ToString("o");

    public static string? GetUpdatedAt([Parent] Field field) =>
        field.UpdatedAt.ToUniversalTime().ToString("o");

    public static async Task<Collection?> GetCollection(
        [Parent] Field field,
        [Service] TechtonicCmsDbContext db)
    {
        return await db.Collections.FindAsync(field.CollectionId);
    }
}
