using System.Text.Json;
using Azure.Messaging.ServiceBus;
using DistributedCrm.Application.UseCases.ReturnRequests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DistributedCrm.Infrastructure.Messaging;

// Retry design (maps the Phase 4 RabbitMQ topology onto Service Bus):
// - processing failure -> schedule a copy of the message RetryDelay from now with retry-count + 1, then complete the original
// - retry-count reaches MaxRetries -> explicit dead-letter ("MaxRetriesExceeded")
// - malformed payload -> explicit dead-letter ("MalformedPayload"), no retry
// - worker crash / lost lock -> Service Bus redelivers; the queue's MaxDeliveryCount dead-letters a message that keeps doing that
public sealed class ReturnRequestConsumer : IAsyncDisposable
{
    // Defined outside the app: distributed_crm_infra/servicebus/Config.json locally, Bicep in Azure.
    // be sends to the same name - the one coupling point between the two repos.
    public const string QueueName = "return-requests";

    private const string RetryCountProperty = "retry-count";
    private const int MaxRetries = 3;
    private const int MaxDeadLetterDescriptionLength = 1024;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(30);
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly ServiceBusProcessor _processor;
    private readonly ServiceBusSender _retrySender;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ReturnRequestConsumer> _logger;

    public ReturnRequestConsumer(
        ServiceBusClient client,
        IServiceScopeFactory scopeFactory,
        ILogger<ReturnRequestConsumer> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;

        _processor = client.CreateProcessor(QueueName, new ServiceBusProcessorOptions
        {
            AutoCompleteMessages = false, // CRITICAL - commit to Postgres first, then complete
            MaxConcurrentCalls = 1, // one message at a time, like prefetchCount: 1 in Phase 4
        });
        _processor.ProcessMessageAsync += HandleMessageAsync;
        _processor.ProcessErrorAsync += HandleErrorAsync;

        _retrySender = client.CreateSender(QueueName);
    }

    public async Task RunAsync(CancellationToken stoppingToken)
    {
        await _processor.StartProcessingAsync(stoppingToken);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
        }

        // Lets the in-flight message finish (and complete) before shutdown, instead of abandoning it mid-commit.
        await _processor.StopProcessingAsync(CancellationToken.None);
    }

    private async Task HandleMessageAsync(ProcessMessageEventArgs args)
    {
        var message = args.Message;
        var ct = args.CancellationToken;

        var returnRequestId = TryReadReturnRequestId(message.Body);
        if (returnRequestId is null)
        {
            // No retry count would ever fix this - park it for a human to look at.
            _logger.LogWarning("Malformed return-request message {MessageId}; dead-lettering without retry.", message.MessageId);
            await args.DeadLetterMessageAsync(
                message,
                deadLetterReason: "MalformedPayload",
                deadLetterErrorDescription: "Body must be JSON {\"returnRequestId\": \"<non-empty guid>\"}.",
                cancellationToken: ct);
            return;
        }

        using (var scope = _scopeFactory.CreateScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<ProcessReturnRequestHandler>();

            try
            {
                await handler.HandleAsync(returnRequestId.Value, ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                // The entity changed underneath us (e.g. user cancelled) - not worth retrying.
                _logger.LogInformation("Return request {ReturnRequestId} changed concurrently; dropping this message.", returnRequestId);
            }
            catch (Exception ex)
            {
                await HandleFailureAsync(args, returnRequestId.Value, ex);
                return;
            }
        }

        // Outside the try: if completing fails (e.g. the lock was lost), the processor abandons the message and
        // Service Bus redelivers it; the handler sees the request is already finished and it completes then.
        await args.CompleteMessageAsync(message, ct);
    }

    private async Task HandleFailureAsync(ProcessMessageEventArgs args, Guid returnRequestId, Exception ex)
    {
        var message = args.Message;
        var ct = args.CancellationToken;
        var retryCount = message.ApplicationProperties.TryGetValue(RetryCountProperty, out var raw) && raw is int count
            ? count
            : 0;

        if (retryCount < MaxRetries)
        {
            var retry = new ServiceBusMessage(message)
            {
                // Must differ from the original's id, or duplicate detection drops the retry as a repeat.
                MessageId = $"return-request-created:{returnRequestId}:retry-{retryCount + 1}",
            };
            retry.ApplicationProperties[RetryCountProperty] = retryCount + 1;

            // Schedule first, then complete: a crash in between means one extra attempt, which the handler
            // tolerates (status guards + xmin). The other order could lose the message entirely.
            await _retrySender.ScheduleMessageAsync(retry, DateTimeOffset.UtcNow.Add(RetryDelay), ct);
            await args.CompleteMessageAsync(message, ct);

            _logger.LogWarning(ex,
                "Processing return request {ReturnRequestId} failed (attempt {Attempt}/{MaxAttempts}); retrying in {RetryDelay}.",
                returnRequestId, retryCount + 1, MaxRetries + 1, RetryDelay);
            return;
        }

        _logger.LogError(ex,
            "Processing return request {ReturnRequestId} failed after {MaxAttempts} attempts; dead-lettering.",
            returnRequestId, MaxRetries + 1);
        await args.DeadLetterMessageAsync(
            message,
            deadLetterReason: "MaxRetriesExceeded",
            deadLetterErrorDescription: Truncate(ex.Message, MaxDeadLetterDescriptionLength),
            cancellationToken: ct);
    }

    private Task HandleErrorAsync(ProcessErrorEventArgs args)
    {
        // Connection/lock errors and exceptions thrown out of HandleMessageAsync. The processor keeps running and
        // reconnects on its own; this is only for visibility.
        _logger.LogError(args.Exception,
            "Service Bus processor error ({ErrorSource}) on {EntityPath}.",
            args.ErrorSource, args.EntityPath);
        return Task.CompletedTask;
    }

    private static Guid? TryReadReturnRequestId(BinaryData body)
    {
        try
        {
            var payload = body.ToObjectFromJson<ReturnRequestCreatedMessage>(JsonOptions);
            // A JSON object without the property deserializes to Guid.Empty rather than failing - treat it as malformed.
            return payload is null || payload.ReturnRequestId == Guid.Empty ? null : payload.ReturnRequestId;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

    public async ValueTask DisposeAsync()
    {
        await _processor.DisposeAsync();
        await _retrySender.DisposeAsync();
    }
}

public record ReturnRequestCreatedMessage(Guid ReturnRequestId);
