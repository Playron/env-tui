namespace ContactExtractor.Api.Domain;

public class DuplicateGroup
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public string UserId { get; private set; } = "anonymous";
    public double Similarity { get; private set; }
    public bool Resolved { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    private readonly List<Contact> _contacts = [];
    public IReadOnlyCollection<Contact> Contacts => _contacts.AsReadOnly();

    private DuplicateGroup() { } // EF Core

    public DuplicateGroup(string userId, double similarity, IEnumerable<Contact> contacts)
    {
        Id = Guid.CreateVersion7();
        UserId = userId;
        Similarity = similarity;
        _contacts.AddRange(contacts);
    }

    public void Resolve() => Resolved = true;
}
