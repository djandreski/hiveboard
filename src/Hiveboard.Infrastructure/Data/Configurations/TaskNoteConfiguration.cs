using Hiveboard.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hiveboard.Infrastructure.Data.Configurations;

public class TaskNoteConfiguration : IEntityTypeConfiguration<TaskNote>
{
    public void Configure(EntityTypeBuilder<TaskNote> builder)
    {
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Content).IsRequired();
        builder.Property(n => n.NoteType).IsRequired();
        builder.Property(n => n.CreatedAt).IsRequired();

        builder.HasOne(n => n.Agent)
            .WithMany(a => a.TaskNotes)
            .HasForeignKey(n => n.AgentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
