using StackExchange.Redis;

using Microsoft.Extensions.Options;

namespace TechtonicCmsApi.Services;

public class RedisService
{
    private readonly ConnectionMultiplexer _multiplexer;
    private readonly IDatabase _db;
    private readonly ILogger<RedisService> _logger;

    public RedisService(IOptions<RedisOptions> options, ILogger<RedisService> logger)
    {
        _logger = logger;
        var config = new ConfigurationOptions
        {
            EndPoints = { $"{options.Value.Host}:{options.Value.Port}" },
            Password = options.Value.Password,
            AbortOnConnectFail = false,
            ConnectRetry = 3,
            ConnectTimeout = 5000,
            SyncTimeout = 5000,
            AsyncTimeout = 10000,
        };

        _multiplexer = ConnectionMultiplexer.Connect(config);
        _db = _multiplexer.GetDatabase();
    }

    public IDatabase Database => _db;
}

public class RedisOptions
{
    public required string Host { get; set; } = "localhost";
    public required int Port { get; set; } = 6379;
    public required string Password { get; set; } = "";
}
