using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DistributedCrm.Infrastructure.HealthChecks;

// Peeking proves the connection, the credentials and that the queue exists. It needs only Listen rights,
// never locks or removes a message, and returns immediately on an empty queue.
public sealed class ServiceBusQueueHealthCheck : IHealthCheck, IAsyncDisposable
{
    private readonly ServiceBusReceiver _receiver;

    public ServiceBusQueueHealthCheck(ServiceBusClient client, string queueName)
    {
        _receiver = client.CreateReceiver(queueName);
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            await _receiver.PeekMessageAsync(cancellationToken: ct);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, exception: ex);
        }
    }

    public ValueTask DisposeAsync() => _receiver.DisposeAsync();
}
