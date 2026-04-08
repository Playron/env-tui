namespace ContactExtractor.Api.Domain;

public class Tag
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public string Name { get; set; } = string.Empty;    // "Kurs mai 2026", "HR-avdeling"
    public string? Color { get; set; }            // Hex-farge for UI, e.g. "#3B82F6"
    public string UserId { get; private set; } = "anonymous";
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    private readonly List<Contact> _contacts = [];
    public IReadOnlyCollection<Contact> Contacts => _contacts.AsReadOnly();

    private Tag() { } // EF Core

    public Tag(string name, string? color, string userId)
    {
        Id = Guid.CreateVersion7();
        Name = name;
        Color = color;
        UserId = userId;
    }
}
