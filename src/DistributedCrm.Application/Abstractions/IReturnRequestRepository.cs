using DistributedCrm.Domain.Entities;

namespace DistributedCrm.Application.Abstractions;

public interface IReturnRequestRepository
{
    Task<ReturnRequest?> GetByIdAsync(Guid id, CancellationToken ct = default);
}