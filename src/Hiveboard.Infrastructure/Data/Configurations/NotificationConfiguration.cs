using Hiveboard.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hiveboard.Infrastructure.Data.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Type).IsRequired();
        builder.Property(n => n.Message).IsRequired();
        builder.Property(n => n.IsAcknowledged).IsRequired();
        builder.Property(n => n.CreatedAt).IsRequired();

        builder.HasIndex(n => new { n.AgentId, n.IsAcknowledged });
    }
}
