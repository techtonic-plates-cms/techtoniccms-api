using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using TechtonicCmsApi.Contexts;
using TechtonicCmsApi.Schema.TechtonicCms;
using TechtonicCmsApi.Security;
using TechtonicCmsApi.Services;
using TechtonicCmsApi.Types.Assets;
using TechtonicCmsApi.Types.Collections.DynamicCollections;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables();

var connectionString = $"Host={builder.Configuration["Database:Url"]};Port={builder.Configuration["Database:Port"]};Username={builder.Configuration["Database:User"]};Password={builder.Configuration["Database:Password"]};Database={builder.Configuration["Database:Name"]}";

builder.Services.AddDbContext<TechtonicCmsDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddSingleton<RedisService>();
builder.Services.AddSingleton<CollectionTypeModule>();
builder.Services.AddScoped<SessionService>();
builder.Services.AddScoped<PasswordService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<AbacService>();
builder.Services.AddScoped<S3Service>();

builder.Services.AddOptions<RedisOptions>()
    .Bind(builder.Configuration.GetSection("Redis"));
builder.Services.AddOptions<S3Options>()
    .Bind(builder.Configuration.GetSection("S3"));
builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection("Jwt"));

var rsaPublicKeyPem = builder.Configuration["Jwt:RsaPublicKeyPem"];
var rsaPrivateKeyPem = builder.Configuration["Jwt:RsaPrivateKeyPem"];
var issuer = builder.Configuration["Jwt:Issuer"] ?? "techtonic-cms";

var rsa = System.Security.Cryptography.RSA.Create();
if (!string.IsNullOrWhiteSpace(rsaPrivateKeyPem))
    rsa.ImportFromPem(rsaPrivateKeyPem);
else if (!string.IsNullOrWhiteSpace(rsaPublicKeyPem))
    rsa.ImportFromPem(rsaPublicKeyPem);

var rsaKey = new RsaSecurityKey(rsa);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = false,
            ValidateLifetime = true,
            IssuerSigningKey = rsaKey,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.Zero,
        };
    });

builder.Services.AddAuthorization(options =>
{
    SecurityPolicies.Register(options);
});

builder.Services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, AbacAuthorizationHandler>();

builder.Services.AddHttpContextAccessor();

builder.AddGraphQL()
    .AddAuthorization()
    .AddProjections()
    .AddFiltering()
    .AddSorting()
    .AddPagingArguments()
    .ModifyOptions(o => o.EnableOneOf = true)
    .ModifyRequestOptions(options =>
    {
        options.IncludeExceptionDetails = builder.Environment.IsDevelopment();
    })
    .AddTypeModule<CollectionTypeModule>()
    .AddTypes();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<TechtonicCmsDbContext>();
    dbContext.Database.Migrate();

    var passwordService = scope.ServiceProvider.GetRequiredService<PasswordService>();
    var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    await AdminBootstrapService.SeedAsync(dbContext, passwordService, config);
}

app.UseSecurityHeaders();

app.UseAuthentication();
app.UseAuthorization();

app.MapAssetEndpoints();

app.MapGraphQL();

app.RunWithGraphQLCommands(args);
