using DistributedCrm.Application.Abstractions;
using DistributedCrm.Domain.Enums;

namespace DistributedCrm.Application.UseCases.ReturnRequests;


public class ProcessReturnRequestHandler
{
    private readonly IReturnRequestRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IApprovalApiClient _approvalApi;
    private readonly IReturnRequestCacheInvalidator _cacheInvalidator;

    public ProcessReturnRequestHandler(
        IReturnRequestRepository repository,
        IUnitOfWork unitOfWork,
        IApprovalApiClient approvalApi,
        IReturnRequestCacheInvalidator cacheInvalidator)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _approvalApi = approvalApi;
        _cacheInvalidator = cacheInvalidator;
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
            await _cacheInvalidator.InvalidateAsync(request.Id, ct);
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
        await _cacheInvalidator.InvalidateAsync(request.Id, ct);
        return ProcessOutcome.Processed;
    }
}