using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ContactExtractor.Api.Infrastructure.Configurations;

public class WebhookConfigConfiguration : IEntityTypeConfiguration<WebhookConfig>
{
    public void Configure(EntityTypeBuilder<WebhookConfig> builder)
    {
        builder.HasKey(w => w.Id);
        builder.Property(w => w.UserId).HasMaxLength(128).IsRequired();
        builder.Property(w => w.Url).HasMaxLength(500).IsRequired();
        builder.Property(w => w.Event).HasMaxLength(100).IsRequired();
        builder.Property(w => w.Secret).HasMaxLength(256);
        builder.Property(w => w.IsActive).HasDefaultValue(true);
        builder.Property(w => w.CreatedAt).IsRequired();

        builder.HasIndex(w => w.UserId);
    }
}
