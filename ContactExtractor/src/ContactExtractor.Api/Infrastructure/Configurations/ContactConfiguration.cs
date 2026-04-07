using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ContactExtractor.Api.Infrastructure.Configurations;

public class ContactConfiguration : IEntityTypeConfiguration<Contact>
{
    public void Configure(EntityTypeBuilder<Contact> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.FirstName).HasMaxLength(100);
        builder.Property(c => c.LastName).HasMaxLength(100);
        builder.Property(c => c.FullName).HasMaxLength(200);
        builder.Property(c => c.Email).HasColumnName("Email").HasMaxLength(320);
        builder.Property(c => c.Phone).HasColumnName("Phone").HasMaxLength(30);
        builder.Property(c => c.PhoneCountryCode).HasColumnName("PhoneCountryCode").HasMaxLength(5);
        builder.Property(c => c.Organization).HasMaxLength(200);
        builder.Property(c => c.Title).HasMaxLength(100);
        builder.Property(c => c.Address).HasMaxLength(500);
        builder.Property(c => c.Confidence).HasColumnType("REAL");
        builder.Property(c => c.ExtractionSource).HasMaxLength(10).HasDefaultValue("regex");
    }
}
