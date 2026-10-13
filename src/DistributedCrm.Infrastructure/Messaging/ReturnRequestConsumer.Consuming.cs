using System.Text.Json;
using DistributedCrm.Application.UseCases.ReturnRequests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace DistributedCrm.Infrastructure.Messaging;

public partial class ReturnRequestConsumer
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task StartConsumingAsync(CancellationToken ct = default)
    {
        var channel = await _connection.CreateChannelAsync(cancellationToken: ct);

        // only handle 1 message at a time
        await channel.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: 1,
            global: false,
            cancellationToken: ct);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, ea) => await HandleMessageAsync(channel, ea, ct);

        await channel.BasicConsumeAsync(
            queue: QueueName,
            autoAck: false, // CRITICAL - commit then ack - dont delete this message until it is completed committed (finished)
            consumer: consumer,
            cancellationToken: ct);
    }

    private async Task HandleMessageAsync(IChannel channel, BasicDeliverEventArgs ea, CancellationToken ct)
    {
        var payload = JsonSerializer.Deserialize<ReturnRequestCreatedMessage>(ea.Body.Span, JsonOptions);

        using var scope = _scopeFactory.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<ProcessReturnRequestHandler>();

        try
        {
            await handler.HandleAsync(payload!.ReturnRequestId, ct);
            await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            //the entity changed underneath us (e.g. user cancelled) - not worth retrying
            await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: ct);
        }
        catch (Exception)
        {
            await HandleFailureAsync(channel, ea, ct);
        }
    }

    private async Task HandleFailureAsync(IChannel channel, BasicDeliverEventArgs ea, CancellationToken ct)
    {
        var retryCount = GetRetryCount(ea.BasicProperties);

        if (retryCount < MaxRetries)
        {
            await channel.BasicNackAsync(
                ea.DeliveryTag,
                multiple: false,
                requeue: false,
                cancellationToken: ct);
            return;
        }

        var properties = new BasicProperties
        {
            Persistent = true,
            Headers = ea.BasicProperties.Headers
        };

        await channel.BasicPublishAsync(
            exchange: DlxExchangeName,
            routingKey: RoutingKey,
            mandatory: false,
            basicProperties: properties,
            body: ea.Body,
            cancellationToken: ct);

        await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: ct);
    }

    private static int GetRetryCount(IReadOnlyBasicProperties properties)
    {
        if (properties.Headers is null || !properties.Headers.TryGetValue("x-death", out var raw))
            return 0;

        if (raw is not List<object> deaths)
            return 0;

        var total = 0;
        foreach (var entry in deaths)
        {
            if (entry is Dictionary<string, object> death
                && death.TryGetValue("queue", out var queueObj)
                && queueObj is byte[] queueBytes
                && System.Text.Encoding.UTF8.GetString(queueBytes) == QueueName
                && death.TryGetValue("count", out var countObj)
                && countObj is long count)
            {
                total += (int)count;
            }
        }

        return total;
    }
}

public record ReturnRequestCreatedMessage(Guid ReturnRequestId);