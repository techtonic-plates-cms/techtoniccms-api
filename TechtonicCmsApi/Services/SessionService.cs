using System.Text.Json;
using StackExchange.Redis;
using TechtonicCmsApi.Schema.TechtonicCms.Enums;

namespace TechtonicCmsApi.Services;

public record SessionData(string UserId, string UserName, DateTime CreatedAt, DateTime ExpiresAt, UserStatus Status);

public record RefreshTokenData(string UserId, string SessionId, DateTime CreatedAt, DateTime ExpiresAt);

public class SessionService
{
    private static readonly TimeSpan SessionTtl = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan RefreshTokenTtl = TimeSpan.FromDays(30);

    private readonly IDatabase _db;

    public SessionService(RedisService redisService)
    {
        _db = redisService.Database;
    }

    public async Task<SessionData> CreateSessionAsync(string sessionId, string userId, string userName, UserStatus status)
    {
        var now = DateTime.UtcNow;
        var session = new SessionData(userId, userName, now, now.Add(SessionTtl), status);
        var json = JsonSerializer.Serialize(session);

        var batch = _db.CreateBatch();
        var setTask = batch.StringSetAsync($"session:{sessionId}", json, SessionTtl);
        var saddTask = batch.SetAddAsync($"user:sessions:{userId}", sessionId);
        batch.Execute();

        await setTask;
        await saddTask;

        return session;
    }

    public async Task<SessionData?> GetSessionAsync(string sessionId)
    {
        var json = await _db.StringGetAsync($"session:{sessionId}");

        if (!json.HasValue)
            return null;

        return JsonSerializer.Deserialize<SessionData>((string)json!)!;
    }

    public async Task<SessionData?> RefreshSessionAsync(string sessionId)
    {
        var existing = await GetSessionAsync(sessionId);

        if (existing is null)
            return null;

        var now = DateTime.UtcNow;
        var refreshed = existing with { ExpiresAt = now.Add(SessionTtl) };
        var json = JsonSerializer.Serialize(refreshed);

        await _db.StringSetAsync($"session:{sessionId}", json, SessionTtl);

        return refreshed;
    }

    public async Task DeleteSessionAsync(string sessionId, string userId)
    {
        var batch = _db.CreateBatch();
        var delTask = batch.KeyDeleteAsync($"session:{sessionId}");
        var sremTask = batch.SetRemoveAsync($"user:sessions:{userId}", sessionId);
        batch.Execute();

        await delTask;
        await sremTask;
    }

    public async Task<RefreshTokenData> CreateRefreshTokenAsync(string refreshTokenId, string userId, string sessionId)
    {
        var now = DateTime.UtcNow;
        var token = new RefreshTokenData(userId, sessionId, now, now.Add(RefreshTokenTtl));
        var json = JsonSerializer.Serialize(token);

        await _db.StringSetAsync($"refresh:{refreshTokenId}", json, RefreshTokenTtl);

        return token;
    }

    public async Task<RefreshTokenData?> GetRefreshTokenAsync(string refreshTokenId)
    {
        var json = await _db.StringGetAsync($"refresh:{refreshTokenId}");

        if (!json.HasValue)
            return null;

        return JsonSerializer.Deserialize<RefreshTokenData>((string)json!)!;
    }

    public async Task DeleteRefreshTokenAsync(string refreshTokenId)
    {
        await _db.KeyDeleteAsync($"refresh:{refreshTokenId}");
    }

    public async Task DeleteAllUserSessionsAsync(string userId)
    {
        var sessionIds = await _db.SetMembersAsync($"user:sessions:{userId}");

        var tasks = new List<Task>();

        foreach (var sessionId in sessionIds)
        {
            tasks.Add(_db.KeyDeleteAsync($"session:{sessionId}"));
        }

        tasks.Add(_db.KeyDeleteAsync($"user:sessions:{userId}"));

        await Task.WhenAll(tasks);
    }
}
