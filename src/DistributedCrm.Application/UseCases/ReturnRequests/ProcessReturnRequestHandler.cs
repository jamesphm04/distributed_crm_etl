using DistributedCrm.Application.Abstractions;
using DistributedCrm.Domain.Enums;

namespace DistributedCrm.Application.UseCases.ReturnRequests;


public class ProcessReturnRequestHandler
{
    private readonly IReturnRequestRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IApprovalApiClient _approvalApi;

    public ProcessReturnRequestHandler(
        IReturnRequestRepository repository,
        IUnitOfWork unitOfWork,
        IApprovalApiClient approvalApi)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _approvalApi = approvalApi;
    }

    public async Task<ProcessOutcome> HandleAsync(Guid returnRequestId, CancellationToken ct = default)
    {
        var request = await _repository.GetByIdAsync(returnRequestId, ct);

        if (request is null)
            return ProcessOutcome.NotFound;

        if (request.IsFinished)
            return ProcessOutcome.AlreadyFinished;

        if (request.Status == ReturnRequestStatus.Pending)
        {
            request.MarkProcessing();
            await _unitOfWork.SaveChangesAsync(ct);
        }

        var decision = await _approvalApi.RequestDecisionAsync(
            request.Id,
            request.OrderReference,
            request.Item,
            ct);

        if (decision == ApprovalDecision.Approved)
            request.MarkApproved();
        else 
            request.MarkRejected();

        await _unitOfWork.SaveChangesAsync(ct);
        return ProcessOutcome.Processed;
    }
}