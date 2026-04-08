namespace ContactExtractor.Api.Domain;

public class AuditLogEntry
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public string UserId { get; private set; } = string.Empty;
    public string Action { get; private set; } = string.Empty;      // "upload", "export", "delete", "merge_duplicates"
    public string EntityType { get; private set; } = string.Empty;  // "UploadSession", "Contact"
    public Guid? EntityId { get; private set; }
    public string? Details { get; private set; }                    // JSON med ekstra info
    public DateTime Timestamp { get; private set; } = DateTime.UtcNow;

    private AuditLogEntry() { } // EF Core

    public static AuditLogEntry Create(
        string userId,
        string action,
        string entityType,
        Guid? entityId = null,
        string? details = null) =>
        new()
        {
            UserId     = userId,
            Action     = action,
            EntityType = entityType,
            EntityId   = entityId,
            Details    = details,
            Timestamp  = DateTime.UtcNow
        };
}
