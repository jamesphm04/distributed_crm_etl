using DistributedCrm.Application.Abstractions;
using DistributedCrm.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DistributedCrm.Infrastructure.Persistence;

public class ReturnRequestRepository : IReturnRequestRepository
{
    private readonly WorkerDbContext _dbContext;

    public ReturnRequestRepository(WorkerDbContext dbContext)
    {
        _dbContext = dbContext;
    }
        
    public async Task<ReturnRequest?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _dbContext.ReturnRequests
            .FirstOrDefaultAsync(r => r.Id == id, ct);
    }
}