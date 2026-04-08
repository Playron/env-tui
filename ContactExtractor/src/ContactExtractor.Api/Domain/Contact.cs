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
    public bool IsValidEmail { get; set; }                   // Fase 5
    public bool IsValidPhone { get; set; }                   // Fase 5
    public Guid? DuplicateGroupId { get; set; }              // Fase 5

    private readonly List<Tag> _tags = [];                   // Fase 5
    public IReadOnlyCollection<Tag> Tags => _tags.AsReadOnly();

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

    public void AddTag(Tag tag)
    {
        if (!_tags.Any(t => t.Id == tag.Id))
            _tags.Add(tag);
    }

    public void RemoveTag(Guid tagId)
    {
        var tag = _tags.FirstOrDefault(t => t.Id == tagId);
        if (tag is not null) _tags.Remove(tag);
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
        ExtractionSource,
        IsValidEmail,
        IsValidPhone,
        _tags.Select(t => new TagDto(t.Id, t.Name, t.Color)).ToList());

    private string? BuildFullName()
    {
        if (FirstName is null && LastName is null) return null;
        return $"{FirstName} {LastName}".Trim();
    }
}
