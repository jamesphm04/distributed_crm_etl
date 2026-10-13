namespace DistributedCrm.Application.Abstractions;

public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken ct = default);
}