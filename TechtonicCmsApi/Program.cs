using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using TechtonicCmsApi.Contexts;
using TechtonicCmsApi.Schema.TechtonicCms;
using TechtonicCmsApi.Schema.TechtonicCms.Enums;
using TechtonicCmsApi.Security;
using TechtonicCmsApi.Services;
using TechtonicCmsApi.Types.Assets;
using TechtonicCmsApi.Types.Collections.DynamicCollections;
using TechtonicCmsApi.Types.Llms;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables();

var connectionString = $"Host={builder.Configuration["Database:Url"]};Port={builder.Configuration["Database:Port"]};Username={builder.Configuration["Database:User"]};Password={builder.Configuration["Database:Password"]};Database={builder.Configuration["Database:Name"]}";

builder.Services.AddPooledDbContextFactory<TechtonicCmsDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddSingleton<RedisService>();
builder.Services.AddSingleton<CollectionTypeModule>();
builder.Services.AddScoped<SessionService>();
builder.Services.AddScoped<PasswordService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<AbacService>();
builder.Services.AddScoped<S3Service>();
builder.Services.AddScoped<ApiKeyService>();

builder.Services.AddOptions<RedisOptions>()
    .Bind(builder.Configuration.GetSection("Redis"));
builder.Services.AddOptions<S3Options>()
    .Bind(builder.Configuration.GetSection("S3"));

if (builder.Environment.IsDevelopment())
{
    builder.Configuration["Jwt:AccessTokenTtlMinutes"] = "1440"; // 1 day
}

builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection("Jwt"));

var rsaPublicKeyPem = builder.Configuration["Jwt:RsaPublicKeyPem"];
var rsaPrivateKeyPem = builder.Configuration["Jwt:RsaPrivateKeyPem"];
var issuer = builder.Configuration["Jwt:Issuer"] ?? "techtonic-cms";
var audience = builder.Configuration["Jwt:Audience"] ?? "techtonic-cms-api";

var rsa = System.Security.Cryptography.RSA.Create();
if (!string.IsNullOrWhiteSpace(rsaPrivateKeyPem))
    rsa.ImportFromPem(rsaPrivateKeyPem);
else if (!string.IsNullOrWhiteSpace(rsaPublicKeyPem))
    rsa.ImportFromPem(rsaPublicKeyPem);

var rsaKey = new RsaSecurityKey(rsa);

builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("Login", opt =>
    {
        opt.PermitLimit = 10;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });

    options.AddTokenBucketLimiter("Upload", opt =>
    {
        opt.TokenLimit = 10;
        opt.QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
        opt.ReplenishmentPeriod = TimeSpan.FromMinutes(1);
        opt.TokensPerPeriod = 5;
    });

    options.AddFixedWindowLimiter("GeneralApi", opt =>
    {
        opt.PermitLimit = 1000;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });

    options.OnRejected = (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        return ValueTask.CompletedTask;
    };
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateLifetime = true,
            IssuerSigningKey = rsaKey,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.Zero,
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var sessionId = context.Principal?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (sessionId is null)
                {
                    context.Fail("Invalid token: missing session claim");
                    return;
                }

                var sessionService = context.HttpContext.RequestServices.GetRequiredService<SessionService>();
                var session = await sessionService.GetSessionAsync(sessionId);
                if (session is null)
                {
                    context.Fail("Session revoked");
                    return;
                }

                if (session.Status == UserStatus.Inactive)
                {
                    context.Fail("User inactive");
                }
            }
        };
    })
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>("ApiKey", null);

builder.Services.AddAuthorization(options =>
{
    options.DefaultPolicy = new AuthorizationPolicyBuilder()
        .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme, "ApiKey")
        .RequireAuthenticatedUser()
        .Build();
    SecurityPolicies.Register(options);
});

builder.Services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, AbacAuthorizationHandler>();

builder.Services.AddHostedService<SchedulerService>();

builder.Services.AddHttpContextAccessor();

builder.AddGraphQL()
.ModifyCostOptions(options =>
{
    options.MaxFieldCost = 10000;
    options.MaxTypeCost = 1000;
})
.AddMaxExecutionDepthRule(15)
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
    .TryAddTypeInterceptor<CollectionConnectionTypeInterceptor>()
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

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapAssetEndpoints();
app.MapLlmsEndpoints();

app.MapGraphQL().RequireRateLimiting("GeneralApi");

app.MapGet("/healthcheck", () => Results.Ok("healthy"));

app.RunWithGraphQLCommands(args);
