using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace DistributedCrm.Infrastructure.HealthChecks;

// Pings through the app's own multiplexer, so it checks the exact connection requests use.
public sealed class RedisHealthCheck : IHealthCheck
{
    private readonly IConnectionMultiplexer _redis;

    public RedisHealthCheck(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            // PingAsync takes no CancellationToken; the multiplexer's own timeout bounds it.
            await _redis.GetDatabase().PingAsync();
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, exception: ex);
        }
    }
}
