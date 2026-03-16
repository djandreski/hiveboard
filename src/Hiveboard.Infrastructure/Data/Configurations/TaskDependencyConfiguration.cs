using Hiveboard.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hiveboard.Infrastructure.Data.Configurations;

public class TaskDependencyConfiguration : IEntityTypeConfiguration<TaskDependency>
{
    public void Configure(EntityTypeBuilder<TaskDependency> builder)
    {
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Type).IsRequired();

        builder.HasIndex(d => new { d.TaskId, d.DependsOnTaskId }).IsUnique();
    }
}
