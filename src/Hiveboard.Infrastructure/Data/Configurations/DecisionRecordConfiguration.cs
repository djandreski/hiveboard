using Hiveboard.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hiveboard.Infrastructure.Data.Configurations;

public class DecisionRecordConfiguration : IEntityTypeConfiguration<DecisionRecord>
{
    public void Configure(EntityTypeBuilder<DecisionRecord> builder)
    {
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Title).IsRequired().HasMaxLength(200);
        builder.Property(d => d.Content).IsRequired();
        builder.Property(d => d.Status).IsRequired();
        builder.Property(d => d.CreatedAt).IsRequired();

        builder.HasOne(d => d.Agent)
            .WithMany(a => a.DecisionRecords)
            .HasForeignKey(d => d.AgentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
