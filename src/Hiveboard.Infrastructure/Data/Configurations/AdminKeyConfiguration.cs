using Hiveboard.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hiveboard.Infrastructure.Data.Configurations;

public class AdminKeyConfiguration : IEntityTypeConfiguration<AdminKey>
{
    public void Configure(EntityTypeBuilder<AdminKey> builder)
    {
        builder.HasKey(k => k.Id);

        builder.Property(k => k.KeyHash)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(k => k.KeyPrefix)
            .IsRequired()
            .HasMaxLength(20);
    }
}
