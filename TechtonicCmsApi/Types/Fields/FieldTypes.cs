using HotChocolate;
using HotChocolate.Types;

using Microsoft.EntityFrameworkCore;
using TechtonicCmsApi.Contexts;
using TechtonicCmsApi.Schema.TechtonicCms.Entities;
using TechtonicCmsApi.Schema.TechtonicCms.Enums;

namespace TechtonicCmsApi.Types.Fields;

public partial class FieldType : ObjectType<Field>
{
    protected override void Configure(IObjectTypeDescriptor<Field> descriptor)
    {
        descriptor.BindFieldsExplicitly();

        descriptor.Name("Field");

        descriptor.Field(f => f.Id).ID().IsProjected();
        descriptor.Field(f => f.CollectionId).ID().IsProjected();
        descriptor.Field(f => f.Name).Type<NonNullType<StringType>>().IsProjected();
        descriptor.Field(f => f.Label).IsProjected();
        descriptor.Field(f => f.Description).IsProjected();
        descriptor.Field(f => f.DataType).IsProjected();
        descriptor.Field(f => f.IsRequired).IsProjected();
        descriptor.Field(f => f.IsUnique).IsProjected();
       // descriptor.Field(f => f.ValidationRules).IsProjected();
        descriptor.Field(f => f.DefaultValue).IsProjected();
        descriptor.Field(f => f.HelpText).IsProjected();
        descriptor.Field(f => f.RelatedCollectionId).IsProjected();
        descriptor.Field(f => f.CreatedAt).IsProjected();
        descriptor.Field(f => f.UpdatedAt).IsProjected();
    }

    [ExtendObjectType(typeof(FieldType))]
    public class FieldTypeResolvers
    {
        public async Task<Collection?> GetRelatedCollection(
            [Parent] Field field,
            [Service] TechtonicCmsDbContext db)
        {
            if (field.RelatedCollectionId is null) return null;
            return await db.Collections.FindAsync(field.RelatedCollectionId.Value);
        }

        public async Task<Collection?> GetCollection(
            [Parent] Field field,
            [Service] TechtonicCmsDbContext db)
        {
            return await db.Collections.FindAsync(field.CollectionId);
        }
    }
}
