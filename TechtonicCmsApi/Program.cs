using Microsoft.EntityFrameworkCore;
using TechtonicCmsApi.Schema.TechtonicCms;

var builder = WebApplication.CreateBuilder(args);

var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
var databasePort = Environment.GetEnvironmentVariable("DATABASE_PORT");
var databaseUser = Environment.GetEnvironmentVariable("DATABASE_USER");
var databasePassword = Environment.GetEnvironmentVariable("DATABASE_PASSWORD");
var databaseName = Environment.GetEnvironmentVariable("DATABASE_NAME");

var connectionString = $"Host={databaseUrl};Port={databasePort};Username={databaseUser};Password={databasePassword};Database={databaseName}";

builder.Services.AddDbContext<TechtonicCmsDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.AddGraphQL().AddTypes();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<TechtonicCmsDbContext>();
    dbContext.Database.Migrate();
}

app.MapGraphQL();

app.RunWithGraphQLCommands(args);
