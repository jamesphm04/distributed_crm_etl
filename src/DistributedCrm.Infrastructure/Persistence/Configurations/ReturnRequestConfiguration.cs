using DistributedCrm.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DistributedCrm.Infrastructure.Persistence.Configurations;

public class ReturnRequestConfiguration : IEntityTypeConfiguration<ReturnRequest>
{
    public void Configure(EntityTypeBuilder<ReturnRequest> builder)
    {
        builder.ToTable("return_requests");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Status)
            .HasConversion<string>();

        builder.Property<uint>("xmin")
            .IsRowVersion()
            .HasColumnName("xmin");
    }
}
