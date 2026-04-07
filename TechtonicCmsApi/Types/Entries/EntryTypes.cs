using System.Text.Json;
using HotChocolate;
using HotChocolate.Types;

using Microsoft.EntityFrameworkCore;
using TechtonicCmsApi.Contexts;
using TechtonicCmsApi.Schema.TechtonicCms.Entities;

namespace TechtonicCmsApi.Types.Entries;

[ObjectType<Entry>]
public static partial class EntryType
{
    public static string GetId([Parent] Entry entry) => entry.Id.ToString();

    [GraphQLType<NonNullType<StringType>>]
    public static string GetName([Parent] Entry entry) => entry.Name;

    public static string? GetSlug([Parent] Entry entry) => entry.Slug;

    [GraphQLType<NonNullType<StringType>>]
    public static string GetStatus([Parent] Entry entry) => entry.Status.ToString().ToUpperInvariant();

    [GraphQLType<NonNullType<StringType>>]
    public static string GetLocale([Parent] Entry entry) => entry.Locale.ToString().ToUpperInvariant();

    [GraphQLType<NonNullType<StringType>>]
    public static string GetDefaultLocale([Parent] Entry entry) => entry.DefaultLocale.ToString().ToUpperInvariant();

    [GraphQLType<NonNullType<StringType>>]
    public static string GetData([Parent] Entry entry) => entry.Data.RootElement.GetRawText();

    public static string? GetCreatedAt([Parent] Entry entry) =>
        entry.CreatedAt.ToUniversalTime().ToString("o");

    public static string? GetUpdatedAt([Parent] Entry entry) =>
        entry.UpdatedAt.ToUniversalTime().ToString("o");

    public static string? GetPublishedAt([Parent] Entry entry) =>
        entry.PublishedAt?.ToUniversalTime().ToString("o");

    public static string GetCollectionId([Parent] Entry entry) => entry.CollectionId.ToString();

    public static string GetCreatedBy([Parent] Entry entry) => entry.CreatedBy.ToString();

    public static async Task<Collection?> GetCollection(
        [Parent] Entry entry,
        [Service] TechtonicCmsDbContext db)
    {
        return await db.Collections.FindAsync(entry.CollectionId);
    }
}
