namespace DistributedCrm.Application.Abstractions;

public interface IReturnRequestCacheInvalidator
{
    Task InvalidateAsync(Guid returnRequestId, CancellationToken ct = default);
}
