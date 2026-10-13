using DistributedCrm.Application.Abstractions;
using DistributedCrm.Application.Exceptions;
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
        var latency = Random.Shared.Next(_options.MinLatencyMs, _options.MaxLatencyMs + 1);
        await Task.Delay(latency, ct);

        if (Random.Shared.NextDouble() < _options.FailureRate)
            throw new ApprovalApiException($"Simulated approval API failure for return request {returnRequestId}.");

        return Random.Shared.NextDouble() < _options.ApprovalRate
            ? ApprovalDecision.Approved
            : ApprovalDecision.Rejected;
    }
}