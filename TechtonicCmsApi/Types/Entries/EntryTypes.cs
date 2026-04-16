using System.Text.Json;
using HotChocolate;
using HotChocolate.Types;

using Microsoft.EntityFrameworkCore;
using TechtonicCmsApi.Contexts;
using TechtonicCmsApi.Schema.TechtonicCms.Entities;
using TechtonicCmsApi.Schema.TechtonicCms.Enums;

namespace TechtonicCmsApi.Types.Entries;

public partial class EntryType : ObjectType<Entry>
{
    protected override void Configure(IObjectTypeDescriptor<Entry> descriptor)
    {
        descriptor.BindFieldsExplicitly();

        descriptor.Name("Entry");

        descriptor.Field(e => e.Id).ID().IsProjected();
        descriptor.Field(e => e.Name).Type<NonNullType<StringType>>().IsProjected();
        descriptor.Field(e => e.Slug).IsProjected();
        descriptor.Field(e => e.Status).IsProjected();
        descriptor.Field(e => e.Locale).IsProjected();
        descriptor.Field(e => e.DefaultLocale).IsProjected();
        descriptor.Field(e => e.CollectionId).ID().IsProjected();
        descriptor.Field(e => e.CreatedBy).ID().IsProjected();
        descriptor.Field(e => e.CreatedAt).IsProjected();
        descriptor.Field(e => e.UpdatedAt).IsProjected();
        descriptor.Field(e => e.PublishedAt).IsProjected();
    }

    [ExtendObjectType(typeof(EntryType))]
    public class EntryTypeResolvers
    {
        [GraphQLType<NonNullType<StringType>>]
        public string GetData([Parent] Entry entry) => entry.Data.RootElement.GetRawText();

        public async Task<Collection?> GetCollection(
            [Parent] Entry entry,
            [Service] TechtonicCmsDbContext db)
        {
            return await db.Collections.FindAsync(entry.CollectionId);
        }
    }
}
