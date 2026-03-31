using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

using TechtonicCmsApi.Schema.TechtonicCms;
using TechtonicCmsApi.Security;
using TechtonicCmsApi.Services;

var builder = WebApplication.CreateBuilder(args);

var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
var databasePort = Environment.GetEnvironmentVariable("DATABASE_PORT");
var databaseUser = Environment.GetEnvironmentVariable("DATABASE_USER");
var databasePassword = Environment.GetEnvironmentVariable("DATABASE_PASSWORD");
var databaseName = Environment.GetEnvironmentVariable("DATABASE_NAME");

var connectionString = $"Host={databaseUrl};Port={databasePort};Username={databaseUser};Password={databasePassword};Database={databaseName}";

builder.Services.AddDbContext<TechtonicCmsDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddSingleton<RedisService>();
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

var rsaPublicKeyPem = Environment.GetEnvironmentVariable("JWT_PUBLIC_KEY")
    ?? builder.Configuration["Jwt:RsaPublicKeyPem"]
    ?? "";
var rsaPrivateKeyPem = Environment.GetEnvironmentVariable("JWT_SECRET_KEY")
    ?? builder.Configuration["Jwt:RsaPrivateKeyPem"];
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

builder.Services.AddGraphQL()
    .AddTypes()
    .AddAuthorization()
    .ModifyRequestOptions(options =>
    {
        options.IncludeExceptionDetails = builder.Environment.IsDevelopment();
    });

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<TechtonicCmsDbContext>();
    dbContext.Database.Migrate();
}

app.UseSecurityHeaders();

app.UseAuthentication();
app.UseAuthorization();

app.MapGraphQL();

app.RunWithGraphQLCommands(args);
