using HotChocolate;
using HotChocolate.Types;

using Microsoft.EntityFrameworkCore;
using TechtonicCmsApi.Contexts;
using TechtonicCmsApi.Schema.TechtonicCms.Entities;

namespace TechtonicCmsApi.Types.ApiKeys;

public class CreateApiKeyPayload
{
    public required ApiKey ApiKey { get; set; }
    public required string Key { get; set; }
}

public partial class CreateApiKeyPayloadType : ObjectType<CreateApiKeyPayload>
{
    protected override void Configure(IObjectTypeDescriptor<CreateApiKeyPayload> descriptor)
    {
        descriptor.BindFieldsExplicitly();
        descriptor.Field(r => r.ApiKey).Type<ApiKeyType>();
        descriptor.Field(r => r.Key).Type<NonNullType<StringType>>();
    }
}

public partial class ApiKeyType : ObjectType<ApiKey>
{
    protected override void Configure(IObjectTypeDescriptor<ApiKey> descriptor)
    {
        descriptor.BindFieldsExplicitly();
        descriptor.Name("ApiKey");

        descriptor.Field(a => a.Id).ID().IsProjected();
        descriptor.Field(a => a.Name).Type<NonNullType<StringType>>().IsProjected();
        descriptor.Field(a => a.KeyPrefix).Type<NonNullType<StringType>>().IsProjected();
        descriptor.Field(a => a.CreatedAt).IsProjected();
        descriptor.Field(a => a.UpdatedAt).IsProjected();
        descriptor.Field(a => a.ExpiresAt).IsProjected();
        descriptor.Field(a => a.IsActive).IsProjected();
        descriptor.Field(a => a.LastUsedAt).IsProjected();
    }

    [ExtendObjectType(typeof(ApiKeyType))]
    public class ApiKeyTypeResolvers
    {
        public async Task<User?> GetUser(
            [Parent] ApiKey apiKey,
            [Service] TechtonicCmsDbContext db)
        {
            return await db.Users.FindAsync(apiKey.UserId);
        }
    }
}
