using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;

using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace TechtonicCmsApi.Services;

public class JwtOptions
{
    public required string Issuer { get; set; } = "techtonic-cms";
    public int AccessTokenTtlMinutes { get; set; } = 15;
    public int RefreshTokenTtlDays { get; set; } = 7;
    public string? RsaPrivateKeyPem { get; set; }
    public string? RsaPublicKeyPem { get; set; }
}

public class AuthService
{
    private readonly JwtOptions _options;
    private readonly SessionService _sessionService;
    private readonly RsaSecurityKey _rsaSecurityKey;
    private readonly SigningCredentials _signingCredentials;
    private readonly TokenValidationParameters _tokenValidationParameters;

    public AuthService(IOptions<JwtOptions> options, SessionService sessionService)
    {
        _options = options.Value;
        _sessionService = sessionService;

        var rsa = RSA.Create();

        if (!string.IsNullOrWhiteSpace(_options.RsaPrivateKeyPem))
        {
            rsa.ImportFromPem(_options.RsaPrivateKeyPem);
        }
        else if (!string.IsNullOrWhiteSpace(_options.RsaPublicKeyPem))
        {
            rsa.ImportFromPem(_options.RsaPublicKeyPem);
        }
        else
        {
            throw new InvalidOperationException("RSA private key or public key PEM must be provided.");
        }

        _rsaSecurityKey = new RsaSecurityKey(rsa);
        _signingCredentials = new SigningCredentials(_rsaSecurityKey, SecurityAlgorithms.RsaSha256);

        _tokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _options.Issuer,
            ValidateAudience = false,
            ValidateLifetime = true,
            IssuerSigningKey = _rsaSecurityKey,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.Zero,
        };
    }

    public async Task<(string Token, string SessionId)> GenerateAccessTokenAsync(Guid userId, string userName)
    {
        var sessionId = Guid.NewGuid().ToString();
        await _sessionService.CreateSessionAsync(sessionId, userId.ToString(), userName);

        var now = DateTime.UtcNow;
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, sessionId),
                new Claim("userId", userId.ToString()),
                new Claim("name", userName),
            ]),
            Issuer = _options.Issuer,
            IssuedAt = now,
            Expires = now.AddMinutes(_options.AccessTokenTtlMinutes),
            SigningCredentials = _signingCredentials,
        };

        var handler = new JwtSecurityTokenHandler();
        var token = handler.CreateEncodedJwt(tokenDescriptor);

        return (token, sessionId);
    }

    public async Task<string> GenerateRefreshTokenAsync(Guid userId, string sessionId)
    {
        var refreshTokenId = Guid.NewGuid().ToString();
        await _sessionService.CreateRefreshTokenAsync(refreshTokenId, userId.ToString(), sessionId);

        var now = DateTime.UtcNow;
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, refreshTokenId),
                new Claim("type", "refresh"),
            ]),
            Issuer = _options.Issuer,
            IssuedAt = now,
            Expires = now.AddDays(_options.RefreshTokenTtlDays),
            SigningCredentials = _signingCredentials,
        };

        var handler = new JwtSecurityTokenHandler();
        return handler.CreateEncodedJwt(tokenDescriptor);
    }

    public ClaimsPrincipal ValidateAccessToken(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        var principal = handler.ValidateToken(token, _tokenValidationParameters, out var validatedToken);

        if (validatedToken is not JwtSecurityToken jwtToken ||
            jwtToken.Header.Alg != SecurityAlgorithms.RsaSha256)
        {
            throw new InvalidOperationException("Invalid token algorithm.");
        }

        var typeClaim = principal.FindFirst("type");
        if (typeClaim is not null && typeClaim.Value == "refresh")
        {
            throw new InvalidOperationException("Refresh tokens cannot be used for access.");
        }

        return principal;
    }

    public SecurityKey GetPublicKey()
    {
        return _rsaSecurityKey;
    }
}
