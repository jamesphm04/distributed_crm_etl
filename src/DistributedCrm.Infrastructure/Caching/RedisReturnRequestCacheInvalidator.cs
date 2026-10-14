using DistributedCrm.Application.Abstractions;
using StackExchange.Redis;

namespace DistributedCrm.Infrastructure.Caching;

// Key format ("returnrequest:{id}") must match DistributedCrm.Infrastructure.Caching.RedisReturnRequestCache
// in the be repo. The two repos share no code (by design, see decisions.md), only this key convention -
// the worker never reads/writes the cache's actual content, only deletes it so be's next GetById is a fresh read.
public class RedisReturnRequestCacheInvalidator : IReturnRequestCacheInvalidator
{
    private readonly IConnectionMultiplexer _redis;

    public RedisReturnRequestCacheInvalidator(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task InvalidateAsync(Guid returnRequestId, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        await db.KeyDeleteAsync($"returnrequest:{returnRequestId}");
    }
}
