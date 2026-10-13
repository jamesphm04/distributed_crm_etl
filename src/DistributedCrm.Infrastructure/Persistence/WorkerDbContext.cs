using DistributedCrm.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DistributedCrm.Infrastructure.Persistence;

public class WorkerDbContext: DbContext
{
    public WorkerDbContext(DbContextOptions<WorkerDbContext> options) : base(options)
    {
    }

    public DbSet<ReturnRequest> ReturnRequests => Set<ReturnRequest>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(WorkerDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}