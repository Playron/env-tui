namespace ContactExtractor.Api.Domain;

public class UploadSession
{
    public Guid Id { get; private set; }
    public string OriginalFileName { get; private set; } = string.Empty;
    public string FileType { get; private set; } = string.Empty;
    public int TotalRowsProcessed { get; private set; }
    public bool UsedAi { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public string? UserId { get; private set; }  // Keycloak subject ID (Fase 4)

    private readonly List<Contact> _contacts = [];
    public IReadOnlyCollection<Contact> Contacts => _contacts.AsReadOnly();

    public ExtractionStatus Status { get; private set; } = ExtractionStatus.Pending;
    public string? ErrorMessage { get; private set; }

    private UploadSession() { } // EF Core

    public UploadSession(string fileName, string fileType, int rowsProcessed, bool usedAi = false, string? userId = null)
    {
        Id = Guid.CreateVersion7();
        OriginalFileName = fileName;
        FileType = fileType;
        TotalRowsProcessed = rowsProcessed;
        UsedAi = usedAi;
        UserId = userId;
    }

    public void AddContacts(IEnumerable<Contact> contacts) => _contacts.AddRange(contacts);

    public void UpdateStatus(ExtractionStatus status, string? error = null)
    {
        Status = status;
        ErrorMessage = error;
    }

    public void SetRowsProcessed(int count) => TotalRowsProcessed = count;
    public void SetUsedAi(bool usedAi) => UsedAi = usedAi;

    public ExtractionResultDto ToDto(List<string>? warnings = null) => new(
        Id,
        OriginalFileName,
        FileType,
        TotalRowsProcessed,
        _contacts.Count,
        UsedAi,
        _contacts.Select(c => c.ToDto()).ToList(),
        warnings ?? []);
}
