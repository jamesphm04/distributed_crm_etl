using DistributedCrm.Application.Abstractions;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace DistributedCrm.Infrastructure.Caching;

// Key format ("returnrequest:{id}") must match DistributedCrm.Infrastructure.Caching.RedisReturnRequestCache
// in the be repo. The two repos share no code (by design, see decisions.md), only this key convention -
// the worker never reads/writes the cache's actual content, only deletes it so be's next GetById is a fresh read.
//
// Invalidation runs after the status change has committed, so a Redis failure is logged rather than thrown:
// throwing would abandon a message whose work is already done, and the redelivery would find the request finished
// and skip the invalidation anyway. The entry is at most one TTL (5 minutes, set in be) stale.
public class RedisReturnRequestCacheInvalidator : IReturnRequestCacheInvalidator
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisReturnRequestCacheInvalidator> _logger;

    public RedisReturnRequestCacheInvalidator(IConnectionMultiplexer redis, ILogger<RedisReturnRequestCacheInvalidator> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task InvalidateAsync(Guid returnRequestId, CancellationToken ct = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            await db.KeyDeleteAsync($"returnrequest:{returnRequestId}");
        }
        // RedisTimeoutException derives from TimeoutException, not RedisException, so both are needed.
        catch (Exception ex) when (ex is RedisException or RedisTimeoutException)
        {
            _logger.LogWarning(ex,
                "Cache invalidation for return request {ReturnRequestId} failed; the entry may be stale until it expires.",
                returnRequestId);
        }
    }
}
