namespace DistributedCrm.Application.Abstractions;


public interface IApprovalApiClient
{
    Task<ApprovalDecision> RequestDecisionAsync(
        Guid returnRequestId,
        string orderReference,
        string item,
        CancellationToken ct = default);
}