using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;

namespace DistributedCrm.Infrastructure.Messaging;

public partial class ReturnRequestConsumer
{
    private const string ExchangeName = "return-requests";
    private const string RetryExchangeName = "return-requests.retry";
    private const string DlxExchangeName = "return-requests.dlx";

    private const string QueueName = "return-requests.process";
    private const string RetryQueueName = "return-requests.retry";
    private const string DlqName = "return-requests.dlq";

    private const string RoutingKey = "created";
    private const int RetryTtlMilliseconds = 30_000;
    private const int MaxRetries = 3;

    private readonly IConnection _connection;
    private readonly IServiceScopeFactory _scopeFactory;

    public ReturnRequestConsumer(IConnection connection, IServiceScopeFactory scopeFactory)
    {
        _connection = connection;
        _scopeFactory = scopeFactory;
    }

    public static async Task DeclareTopologyAsync(IConnection connection, CancellationToken ct = default)
    {
        await using var channel = await connection.CreateChannelAsync(cancellationToken: ct);
    
        // Define main exchange + main queue
        await channel.ExchangeDeclareAsync(
            ExchangeName, 
            ExchangeType.Direct, 
            durable: true, 
            cancellationToken: ct);
        await channel.QueueDeclareAsync(
            queue: QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = RetryExchangeName
            },
            cancellationToken: ct);
        await channel.QueueBindAsync(QueueName, ExchangeName, RoutingKey, cancellationToken: ct);
    
        // Define retry exchange + retry queue
        await channel.ExchangeDeclareAsync(
            RetryExchangeName,
            ExchangeType.Direct,
            durable: true,
            cancellationToken: ct);
        await channel.QueueDeclareAsync(
            queue: RetryQueueName,
            durable: true,
            autoDelete: false,
            exclusive: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = ExchangeName,
                ["x-message-ttl"] = RetryTtlMilliseconds
            },
            cancellationToken: ct);
        await channel.QueueBindAsync(RetryQueueName, RetryExchangeName, RoutingKey, cancellationToken: ct);
    
        // Define dead letter exchange and dead letter queue
        await channel.ExchangeDeclareAsync(
            DlxExchangeName,
            ExchangeType.Direct,
            durable: true,
            cancellationToken: ct);

        await channel.QueueDeclareAsync(
            queue: DlqName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: ct);
        await channel.QueueBindAsync(DlqName, DlxExchangeName, RoutingKey, cancellationToken: ct);
    }

}