using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TechtonicCmsApi.Contexts;
using TechtonicCmsApi.Schema.TechtonicCms.Enums;
using TechtonicCmsApi.Services;

namespace TechtonicCmsApi.Security;

public class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly IDbContextFactory<TechtonicCmsDbContext> _dbContextFactory;
    private readonly ApiKeyService _apiKeyService;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IDbContextFactory<TechtonicCmsDbContext> dbContextFactory,
        ApiKeyService apiKeyService)
        : base(options, logger, encoder)
    {
        _dbContextFactory = dbContextFactory;
        _apiKeyService = apiKeyService;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Api-Key", out var headerValue))
        {
            return AuthenticateResult.NoResult();
        }

        var rawKey = headerValue.ToString();
        var hash = _apiKeyService.ComputeHash(rawKey);

        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var apiKey = await db.ApiKeys
            .Include(a => a.User)
            .FirstOrDefaultAsync(a => a.KeyHash == hash && a.IsActive);

        if (apiKey is null)
        {
            return AuthenticateResult.Fail("Invalid API key");
        }

        if (apiKey.ExpiresAt.HasValue && apiKey.ExpiresAt.Value < DateTime.UtcNow)
        {
            return AuthenticateResult.Fail("API key expired");
        }

        if (apiKey.User.Status != UserStatus.Active)
            return AuthenticateResult.Fail("User inactive");

        apiKey.LastUsedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var claims = new[]
        {
            new Claim("userId", apiKey.UserId.ToString()),
            new Claim("name", apiKey.User.Name),
            new Claim(ClaimTypes.NameIdentifier, apiKey.UserId.ToString()),
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return AuthenticateResult.Success(ticket);
    }
}
