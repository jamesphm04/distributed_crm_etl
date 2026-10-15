using System.Diagnostics;
using DistributedCrm.Application.Abstractions;
using DistributedCrm.Application.Exceptions;
using DistributedCrm.Infrastructure.Telemetry;
using Microsoft.Extensions.Options;

namespace DistributedCrm.Infrastructure.ExternalApis;

public class SimulatedApprovalApiClient : IApprovalApiClient
{
    private readonly SimulatedApprovalApiOptions _options;

    public SimulatedApprovalApiClient(IOptions<SimulatedApprovalApiOptions> options)
    {
        _options = options.Value;
    }

    public async Task<ApprovalDecision> RequestDecisionAsync(
        Guid returnRequestId,
        string orderReference,
        string item,
        CancellationToken ct = default)
    {
        // No HTTP call to instrument, so the span is written by hand: without it the trace shows an unexplained gap
        // where the slowest, flakiest step happens. Client kind: a call out to another system.
        using var activity = InfrastructureTelemetry.ActivitySource.StartActivity(
            "approval-api RequestDecision", ActivityKind.Client);
        activity?.SetTag("return_request.id", returnRequestId);

        var latency = Random.Shared.Next(_options.MinLatencyMs, _options.MaxLatencyMs + 1);
        await Task.Delay(latency, ct);

        if (Random.Shared.NextDouble() < _options.FailureRate)
        {
            var exception = new ApprovalApiException(
                $"Simulated approval API failure for return request {returnRequestId}.");
            activity?.AddException(exception);
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            throw exception;
        }

        var decision = Random.Shared.NextDouble() < _options.ApprovalRate
            ? ApprovalDecision.Approved
            : ApprovalDecision.Rejected;
        activity?.SetTag("approval.decision", decision.ToString());
        return decision;
    }
}