using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ContactExtractor.Api.Infrastructure.Configurations;

public class DuplicateGroupConfiguration : IEntityTypeConfiguration<DuplicateGroup>
{
    public void Configure(EntityTypeBuilder<DuplicateGroup> builder)
    {
        builder.HasKey(d => d.Id);
        builder.Property(d => d.UserId).HasMaxLength(128).IsRequired();
        builder.Property(d => d.Similarity).HasColumnType("REAL");
        builder.Property(d => d.Resolved).HasDefaultValue(false);
        builder.Property(d => d.CreatedAt).IsRequired();

        builder.HasMany(d => d.Contacts)
            .WithOne()
            .HasForeignKey(c => c.DuplicateGroupId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Navigation(d => d.Contacts)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
