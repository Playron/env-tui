namespace ContactExtractor.Api.Domain;

public class Contact
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public Guid UploadSessionId { get; private set; }

    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? FullName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? PhoneCountryCode { get; set; }
    public string? Organization { get; set; }
    public string? Title { get; set; }
    public string? Address { get; set; }
    public double Confidence { get; set; }
    public string ExtractionSource { get; set; } = "regex"; // "regex" | "ai" | "manual"

    private Contact() { } // EF Core

    public Contact(Guid uploadSessionId)
    {
        Id = Guid.CreateVersion7();
        UploadSessionId = uploadSessionId;
    }

    public void SetEmail(EmailAddress? email) => Email = email?.Value;

    public void SetPhone(PhoneNumber? phone)
    {
        Phone = phone?.Value;
        PhoneCountryCode = phone?.CountryCode;
    }

    public ContactDto ToDto() => new(
        Id,
        FirstName,
        LastName,
        FullName ?? BuildFullName(),
        Email,
        Phone,
        Organization,
        Title,
        Address,
        Confidence,
        ExtractionSource);

    private string? BuildFullName()
    {
        if (FirstName is null && LastName is null) return null;
        return $"{FirstName} {LastName}".Trim();
    }
}
