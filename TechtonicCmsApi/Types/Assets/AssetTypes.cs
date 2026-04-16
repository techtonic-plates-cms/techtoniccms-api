using HotChocolate;
using HotChocolate.Types;

using Microsoft.EntityFrameworkCore;

using TechtonicCmsApi.Contexts;
using TechtonicCmsApi.Schema.TechtonicCms.Entities;

namespace TechtonicCmsApi.Types.Assets;

public partial class AssetType : ObjectType<Asset>
{
    protected override void Configure(IObjectTypeDescriptor<Asset> descriptor)
    {
        descriptor.BindFieldsExplicitly();

        descriptor.Name("Asset");

        descriptor.Field(a => a.Id).ID().IsProjected();
        descriptor.Field(a => a.Filename).Type<NonNullType<StringType>>().IsProjected();
        descriptor.Field(a => a.MimeType).Type<NonNullType<StringType>>().IsProjected();
        descriptor.Field(a => a.FileSize).IsProjected();
        descriptor.Field(a => a.Path).Type<NonNullType<StringType>>().IsProjected();
        descriptor.Field(a => a.UploadedBy).ID().IsProjected();
        descriptor.Field(a => a.UploadedAt).IsProjected();
        descriptor.Field(a => a.Alt).IsProjected();
        descriptor.Field(a => a.Caption).IsProjected();
        descriptor.Field(a => a.IsPublic).IsProjected();
    }

    [ExtendObjectType(typeof(AssetType))]
    public class AssetTypeResolvers
    {
        public string GetUrl(
            [Parent] Asset asset,
            [Service] IHttpContextAccessor httpContextAccessor)
        {
            var request = httpContextAccessor.HttpContext?.Request;
            if (request is null) return $"/assets/{asset.Id}";
            return $"{request.Scheme}://{request.Host}/assets/{asset.Id}";
        }

        public async Task<User?> GetUploadedByUser(
            [Parent] Asset asset,
            [Service] TechtonicCmsDbContext db)
        {
            return await db.Users.FindAsync(asset.UploadedBy);
        }
    }
}
