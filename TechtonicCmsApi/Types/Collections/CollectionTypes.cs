using HotChocolate;
using HotChocolate.Types;

using Microsoft.EntityFrameworkCore;
using TechtonicCmsApi.Contexts;
using TechtonicCmsApi.Schema.TechtonicCms.Entities;
using TechtonicCmsApi.Schema.TechtonicCms.Enums;

namespace TechtonicCmsApi.Types.Collections;

[ObjectType<Collection>]
public static partial class CollectionType
{
    public static string GetId([Parent] Collection collection)
    {
        return collection.Id.ToString();
    }

    [GraphQLType<NonNullType<StringType>>]
    public static string GetName([Parent] Collection collection)
    {
        return collection.Name;
    }

    [GraphQLType<NonNullType<StringType>>]
    public static string GetSlug([Parent] Collection collection)
    {
        return collection.Slug;
    }

    public static string? GetDescription([Parent] Collection collection)
    {
        return collection.Description;
    }

    public static string? GetIcon([Parent] Collection collection)
    {
        return collection.Icon;
    }

    public static string? GetColor([Parent] Collection collection)
    {
        return collection.Color;
    }

    public static Locale GetDefaultLocale([Parent] Collection collection)
    {
        return collection.DefaultLocale;
    }

    public static Locale[] GetSupportedLocales([Parent] Collection collection)
    {
        return collection.SupportedLocales.Select(s => Enum.Parse<Locale>(s)).ToArray();
    }

    public static bool GetIsLocalized([Parent] Collection collection)
    {
        return collection.IsLocalized;
    }

    public static string? GetCreatedAt([Parent] Collection collection)
    {
        return collection.CreatedAt.ToUniversalTime().ToString("o");
    }

    public static string? GetUpdatedAt([Parent] Collection collection)
    {
        return collection.UpdatedAt.ToUniversalTime().ToString("o");
    }

    public static async Task<IEnumerable<Field>> GetFields(
        [Parent] Collection collection,
        [Service] TechtonicCmsDbContext db)
    {
        return await db.Fields
            .Where(f => f.CollectionId == collection.Id)
            .OrderBy(f => f.CreatedAt)
            .ToListAsync();
    }

    public static async Task<int> GetEntryCount(
        [Parent] Collection collection,
        [Service] TechtonicCmsDbContext db)
    {
        return await db.Entries.CountAsync(e => e.CollectionId == collection.Id);
    }
}
