using HotChocolate;
using HotChocolate.Types;

using Microsoft.EntityFrameworkCore;
using TechtonicCmsApi.Contexts;
using TechtonicCmsApi.Schema.TechtonicCms.Entities;
using TechtonicCmsApi.Schema.TechtonicCms.Enums;

namespace TechtonicCmsApi.Types.Collections;

public partial class CollectionType : ObjectType<Collection>
{
    protected override void Configure(IObjectTypeDescriptor<Collection> descriptor)
    {
        descriptor.BindFieldsExplicitly();

        descriptor.Name("Collection");

        descriptor.Field(c => c.Id).ID().IsProjected();
        descriptor.Field(c => c.Name).Type<NonNullType<StringType>>().IsProjected();
        descriptor.Field(c => c.Slug).Type<NonNullType<StringType>>().IsProjected();
        descriptor.Field(c => c.Description).IsProjected();
        descriptor.Field(c => c.Icon).IsProjected();
        descriptor.Field(c => c.Color).IsProjected();
        descriptor.Field(c => c.DefaultLocale).IsProjected();
        descriptor.Field(c => c.IsLocalized).IsProjected();
        descriptor.Field(c => c.CreatedAt).IsProjected();
        descriptor.Field(c => c.UpdatedAt).IsProjected();
    }

    [ExtendObjectType(typeof(CollectionType))]
    public class CollectionTypeResolvers
    {
        public Locale[] GetSupportedLocales([Parent] Collection collection)
        {
            return collection.SupportedLocales.Select(s => Enum.Parse<Locale>(s)).ToArray();
        }

        public IQueryable<Field> GetFields(
            [Parent] Collection collection,
            [Service] TechtonicCmsDbContext db)
        {
            return db.Fields
                .Where(f => f.CollectionId == collection.Id)
                .OrderBy(f => f.CreatedAt);
        }

        public async Task<int> GetEntryCount(
            [Parent] Collection collection,
            [Service] TechtonicCmsDbContext db)
        {
            return await db.Entries.CountAsync(e => e.CollectionId == collection.Id);
        }
    }
}
