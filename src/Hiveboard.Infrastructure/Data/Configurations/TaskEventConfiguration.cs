using Hiveboard.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hiveboard.Infrastructure.Data.Configurations;

public class TaskEventConfiguration : IEntityTypeConfiguration<TaskEvent>
{
    public void Configure(EntityTypeBuilder<TaskEvent> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.EventType).IsRequired().HasMaxLength(50);
        builder.Property(e => e.OldValue).HasMaxLength(2000);
        builder.Property(e => e.NewValue).HasMaxLength(2000);
        builder.Property(e => e.Timestamp).IsRequired();

        builder.HasOne(e => e.Agent)
            .WithMany(a => a.TaskEvents)
            .HasForeignKey(e => e.AgentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
