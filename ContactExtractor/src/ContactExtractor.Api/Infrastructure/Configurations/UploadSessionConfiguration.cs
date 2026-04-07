using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ContactExtractor.Api.Infrastructure.Configurations;

public class UploadSessionConfiguration : IEntityTypeConfiguration<UploadSession>
{
    public void Configure(EntityTypeBuilder<UploadSession> builder)
    {
        builder.HasKey(u => u.Id);
        builder.Property(u => u.OriginalFileName).HasMaxLength(256).IsRequired();
        builder.Property(u => u.FileType).HasMaxLength(10).IsRequired();
        builder.Property(u => u.CreatedAt).IsRequired();
        builder.Property(u => u.UsedAi).HasDefaultValue(false);
        builder.Property(u => u.Status)
            .HasConversion<int>()
            .HasDefaultValue(ExtractionStatus.Pending)
            .IsRequired();
        builder.Property(u => u.ErrorMessage).HasMaxLength(1000);

        builder.HasMany(u => u.Contacts)
            .WithOne()
            .HasForeignKey(c => c.UploadSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(u => u.Contacts)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
