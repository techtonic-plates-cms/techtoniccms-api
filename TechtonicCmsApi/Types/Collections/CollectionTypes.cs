using HotChocolate;
using HotChocolate.Types;

using Microsoft.EntityFrameworkCore;
using TechtonicCmsApi.Contexts;
using TechtonicCmsApi.Schema.TechtonicCms.Entities;

namespace TechtonicCmsApi.Types.Collections;

[ObjectType<Collection>]
public static partial class CollectionType
{
    public static string GetId([Parent] Collection collection) => collection.Id.ToString();

    [GraphQLType<NonNullType<StringType>>]
    public static string GetName([Parent] Collection collection) => collection.Name;

    [GraphQLType<NonNullType<StringType>>]
    public static string GetSlug([Parent] Collection collection) => collection.Slug;

    public static string? GetDescription([Parent] Collection collection) => collection.Description;

    public static string? GetIcon([Parent] Collection collection) => collection.Icon;

    public static string? GetColor([Parent] Collection collection) => collection.Color;

    [GraphQLType<NonNullType<StringType>>]
    public static string GetDefaultLocale([Parent] Collection collection) => collection.DefaultLocale.ToString().ToUpperInvariant();

    [GraphQLType<NonNullType<ListType<NonNullType<StringType>>>>]
    public static string[] GetSupportedLocales([Parent] Collection collection) =>
        collection.SupportedLocales.Select(l => l.ToUpperInvariant()).ToArray();

    public static bool GetIsLocalized([Parent] Collection collection) => collection.IsLocalized;

    public static string? GetCreatedAt([Parent] Collection collection) =>
        collection.CreatedAt.ToUniversalTime().ToString("o");

    public static string? GetUpdatedAt([Parent] Collection collection) =>
        collection.UpdatedAt.ToUniversalTime().ToString("o");

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
