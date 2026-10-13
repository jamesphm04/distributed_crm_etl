using DistributedCrm.Domain.Enums;
using DistributedCrm.Domain.Exceptions;

namespace DistributedCrm.Domain.Entities;

public class ReturnRequest
{
    public Guid Id { get; private set; }
    public string OrderReference { get; private set; } = string.Empty;
    public string Item { get; private set; } = string.Empty;
    public ReturnRequestStatus Status { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    
    private ReturnRequest() {}
    
    public bool IsFinished =>
        Status is ReturnRequestStatus.Approved
            or ReturnRequestStatus.Rejected
            or ReturnRequestStatus.Cancelled;

    public void MarkProcessing()
    {
        if (Status != ReturnRequestStatus.Pending)
            throw new InvalidStatusTransitionException($"Cannot mark as Processing from {Status}.");

        Status = ReturnRequestStatus.Processing;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void MarkApproved()
    {
        if (Status != ReturnRequestStatus.Processing) 
            throw new InvalidStatusTransitionException($"Cannot mark as Approved from {Status}.");

        Status = ReturnRequestStatus.Approved;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
    
    public void MarkRejected()
    {
        if (Status != ReturnRequestStatus.Processing)
            throw new InvalidStatusTransitionException($"Cannot mark as Rejected from {Status}.");

        Status = ReturnRequestStatus.Rejected;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
    
}