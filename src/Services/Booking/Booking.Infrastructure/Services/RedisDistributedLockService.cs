using Booking.Domain.Interfaces;
using StackExchange.Redis;

namespace Booking.Infrastructure.Services;

public class RedisDistributedLockService : IDistributedLockService
{
    private readonly IConnectionMultiplexer _redis;

    public RedisDistributedLockService(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task<bool> AcquireLockAsync(string key, string value, TimeSpan expiration)
    {
        var db = _redis.GetDatabase();
        // NX = Not Exists (only set if key doesn't exist)
        return await db.StringSetAsync(key, value, expiration, When.NotExists);
    }

    public async Task ReleaseLockAsync(string key, string value)
    {
        var db = _redis.GetDatabase();
        // Use Lua script to ensure only the owner can release the lock
        string script = @"
            if redis.call('get', KEYS[1]) == ARGV[1] then
                return redis.call('del', KEYS[1])
            else
                return 0
            end";

        await db.ScriptEvaluateAsync(script, new RedisKey[] { key }, new RedisValue[] { value });
    }
}
