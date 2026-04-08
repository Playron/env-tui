using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ContactExtractor.Api.Infrastructure.Configurations;

public class TagConfiguration : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Name).HasMaxLength(100).IsRequired();
        builder.Property(t => t.Color).HasMaxLength(20);
        builder.Property(t => t.UserId).HasMaxLength(128).IsRequired();
        builder.Property(t => t.CreatedAt).IsRequired();

        builder.Navigation(t => t.Contacts)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
