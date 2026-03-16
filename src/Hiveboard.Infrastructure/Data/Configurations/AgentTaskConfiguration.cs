using System.Text.Json;
using Hiveboard.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hiveboard.Infrastructure.Data.Configurations;

public class AgentTaskConfiguration : IEntityTypeConfiguration<AgentTask>
{
    public void Configure(EntityTypeBuilder<AgentTask> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Title).IsRequired().HasMaxLength(200);
        builder.Property(t => t.Description).IsRequired();
        builder.Property(t => t.Status).IsRequired();
        builder.Property(t => t.BlockedReason).HasMaxLength(2000);
        builder.Property(t => t.CreatedAt).IsRequired();
        builder.Property(t => t.UpdatedAt).IsRequired();

        builder.Property(t => t.Metadata)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, (JsonSerializerOptions?)null)
                     ?? new Dictionary<string, string>())
            .HasColumnName("Metadata");

        // Indexes
        builder.HasIndex(t => new { t.ProjectId, t.Status });
        builder.HasIndex(t => t.AssignedAgentId);
        builder.HasIndex(t => t.EpicId);
        builder.HasIndex(t => t.ParentTaskId);

        // Subtasks — restrict to prevent accidental cascade
        builder.HasMany(t => t.Subtasks)
            .WithOne(s => s.ParentTask)
            .HasForeignKey(s => s.ParentTaskId)
            .OnDelete(DeleteBehavior.Restrict);

        // Notes — cascade delete
        builder.HasMany(t => t.Notes)
            .WithOne(n => n.Task)
            .HasForeignKey(n => n.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        // Events — cascade delete
        builder.HasMany(t => t.Events)
            .WithOne(e => e.Task)
            .HasForeignKey(e => e.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        // Dependencies — cascade delete
        builder.HasMany(t => t.Dependencies)
            .WithOne(d => d.Task)
            .HasForeignKey(d => d.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(t => t.DependentTasks)
            .WithOne(d => d.DependsOnTask)
            .HasForeignKey(d => d.DependsOnTaskId)
            .OnDelete(DeleteBehavior.Cascade);

        // Decisions — set null (decisions can exist without task)
        builder.HasMany(t => t.Decisions)
            .WithOne(d => d.Task)
            .HasForeignKey(d => d.TaskId)
            .OnDelete(DeleteBehavior.SetNull);

        // Notifications — cascade delete
        builder.HasMany(t => t.Notifications)
            .WithOne(n => n.Task)
            .HasForeignKey(n => n.TaskId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
