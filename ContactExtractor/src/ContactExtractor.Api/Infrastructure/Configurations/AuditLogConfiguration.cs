using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ContactExtractor.Api.Infrastructure.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLogEntry>
{
    public void Configure(EntityTypeBuilder<AuditLogEntry> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.UserId).HasMaxLength(128).IsRequired();
        builder.Property(a => a.Action).HasMaxLength(50).IsRequired();
        builder.Property(a => a.EntityType).HasMaxLength(50).IsRequired();
        builder.Property(a => a.Details).HasMaxLength(2000);
        builder.Property(a => a.Timestamp).IsRequired();

        builder.HasIndex(a => a.UserId);
        builder.HasIndex(a => a.Timestamp);
    }
}
