using Hiveboard.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hiveboard.Infrastructure.Data.Configurations;

public class AgentConfiguration : IEntityTypeConfiguration<Agent>
{
    public void Configure(EntityTypeBuilder<Agent> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Name).IsRequired().HasMaxLength(200);
        builder.Property(a => a.Type).IsRequired();
        builder.Property(a => a.AgentPlatform).IsRequired();
        builder.Property(a => a.ApiKeyHash).IsRequired().HasMaxLength(64);
        builder.Property(a => a.Status).IsRequired();
        builder.Property(a => a.CreatedAt).IsRequired();

        builder.HasIndex(a => a.ApiKeyHash).IsUnique();

        builder.HasMany(a => a.AssignedTasks)
            .WithOne(t => t.AssignedAgent)
            .HasForeignKey(t => t.AssignedAgentId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(a => a.Notifications)
            .WithOne(n => n.Agent)
            .HasForeignKey(n => n.AgentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
