using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

using HotChocolate.Authorization;

using Microsoft.EntityFrameworkCore;
using TechtonicCmsApi.Contexts;
using TechtonicCmsApi.Schema.TechtonicCms;
using TechtonicCmsApi.Schema.TechtonicCms.Entities;
using TechtonicCmsApi.Services;

namespace TechtonicCmsApi.Types.Auth;

public class AuthMutation
{
    [AllowAnonymous]
    public async Task<LoginPayload> Login(
        string name,
        string password,
        [Service] TechtonicCmsDbContext db,
        [Service] PasswordService passwordService,
        [Service] AuthService authService)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Name == name);
        if (user is null)
            throw new GraphQLException(ErrorBuilder.New()
                .SetMessage("Invalid credentials")
                .SetCode("UNAUTHENTICATED")
                .Build());

        var (isValid, newHash) = passwordService.VerifyPassword(password, user.PasswordHash);
        if (!isValid)
            throw new GraphQLException(ErrorBuilder.New()
                .SetMessage("Invalid credentials")
                .SetCode("UNAUTHENTICATED")
                .Build());

        if (newHash is not null)
        {
            user.PasswordHash = newHash;
            await db.SaveChangesAsync();
        }

        user.LastLoginTime = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var (accessToken, sessionId) = await authService.GenerateAccessTokenAsync(user.Id, user.Name);
        var refreshToken = await authService.GenerateRefreshTokenAsync(user.Id, sessionId);

        var accessTokenExpiry = new DateTime().AddMinutes(15).ToUniversalTime().Subtract(DateTime.UnixEpoch).TotalSeconds;
        var refreshTokenExpiry = new DateTime().AddDays(30).ToUniversalTime().Subtract(DateTime.UnixEpoch).TotalSeconds;

        return new LoginPayload
        {
            AccessToken = new Token { TokenValue = accessToken, ExpiresAt = DateTime.UnixEpoch.AddSeconds(accessTokenExpiry) },
            RefreshToken = new Token { TokenValue = refreshToken, ExpiresAt = DateTime.UnixEpoch.AddSeconds(refreshTokenExpiry) },
            User = user
        };
    }

    [AllowAnonymous]
    public async Task<RefreshPayload> Refresh(
        string refreshToken,
        [Service] AuthService authService,
        [Service] SessionService sessionService)
    {
        ClaimsPrincipal principal;
        try
        {
            principal = authService.ValidateRefreshToken(refreshToken);
        }
        catch
        {
            throw new GraphQLException(ErrorBuilder.New()
                .SetMessage("Invalid refresh token")
                .SetCode("UNAUTHENTICATED")
                .Build());
        }

        var typeClaim = principal.FindFirst("type")?.Value;
        if (typeClaim != "refresh")
            throw new GraphQLException(ErrorBuilder.New()
                .SetMessage("Not a refresh token")
                .SetCode("UNAUTHENTICATED")
                .Build());

        var refreshTokenId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

        if (refreshTokenId is null)
            throw new GraphQLException(ErrorBuilder.New()
                .SetMessage("Invalid refresh token")
                .SetCode("UNAUTHENTICATED")
                .Build());

        var tokenData = await sessionService.GetRefreshTokenAsync(refreshTokenId);
        if (tokenData is null)
            throw new GraphQLException(ErrorBuilder.New()
                .SetMessage("Refresh token not found or expired")
                .SetCode("UNAUTHENTICATED")
                .Build());

        await sessionService.DeleteRefreshTokenAsync(refreshTokenId);

        var session = await sessionService.GetSessionAsync(tokenData.SessionId);
        if (session is null)
            throw new GraphQLException(ErrorBuilder.New()
                .SetMessage("Session not found")
                .SetCode("UNAUTHENTICATED")
                .Build());

        var userId = Guid.Parse(tokenData.UserId);
        var (newAccessToken, newSessionId) = await authService.GenerateAccessTokenAsync(userId, session.UserName);
        var newRefresh = await authService.GenerateRefreshTokenAsync(userId, newSessionId);
        var accessTokenExpiry = new DateTime().AddMinutes(15).ToUniversalTime().Subtract(DateTime.UnixEpoch).TotalSeconds;
        var refreshTokenExpiry = new DateTime().AddDays(30).ToUniversalTime().Subtract(DateTime.UnixEpoch).TotalSeconds;
        await sessionService.DeleteSessionAsync(tokenData.SessionId, tokenData.UserId);

        return new RefreshPayload { AccessToken = new() { TokenValue = newAccessToken, ExpiresAt = DateTime.UnixEpoch.AddSeconds(accessTokenExpiry) }, RefreshToken = new() { TokenValue = newRefresh, ExpiresAt = DateTime.UnixEpoch.AddSeconds(refreshTokenExpiry) } };
    }

    [Authorize]
    public async Task<LogoutPayload> Logout(
        [Service] IHttpContextAccessor httpContextAccessor,
        [Service] AuthService authService,
        [Service] SessionService sessionService)
    {
        var authHeader = httpContextAccessor.HttpContext?.Request.Headers.Authorization.FirstOrDefault();
        if (authHeader is null || !authHeader.StartsWith("Bearer "))
            return new LogoutPayload { Message = "Logged out" };

        var token = authHeader["Bearer ".Length..];
        var principal = authService.ValidateAccessToken(token);
        var sessionId = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        var userIdStr = principal.FindFirst("userId")?.Value;

        if (sessionId is not null && userIdStr is not null)
        {
            await sessionService.DeleteSessionAsync(sessionId, userIdStr);
        }

        return new LogoutPayload { Message = "Logged out" };
    }

    [Authorize]
    public async Task<LogoutPayload> LogoutAll(
        [Service] IHttpContextAccessor httpContextAccessor,
        [Service] AuthService authService,
        [Service] SessionService sessionService)
    {
        var userIdStr = httpContextAccessor.HttpContext?.User.FindFirst("userId")?.Value;
        if (userIdStr is not null)
        {
            await sessionService.DeleteAllUserSessionsAsync(userIdStr);
        }

        return new LogoutPayload { Message = "Logged out from all devices" };
    }
}

[ExtendObjectType(nameof(Mutation))]
public static class AuthMutations
{
    public static AuthMutation Auth() => new();
}
