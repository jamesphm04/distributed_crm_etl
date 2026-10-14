using DistributedCrm.Infrastructure.Messaging;

namespace DistributedCrm.Worker;

public class Worker : BackgroundService
{
    private readonly ReturnRequestConsumer _consumer;

    public Worker(ReturnRequestConsumer consumer)
    {
        _consumer = consumer;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken) => _consumer.RunAsync(stoppingToken);
}
