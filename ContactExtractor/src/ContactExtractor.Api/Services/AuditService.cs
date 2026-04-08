using System.Text.Json;

namespace ContactExtractor.Api.Services;

public class AuditService(AppDbContext db)
{
    public async Task LogAsync(
        string userId,
        string action,
        string entityType,
        Guid? entityId = null,
        object? details = null,
        CancellationToken ct = default)
    {
        var entry = AuditLogEntry.Create(
            userId,
            action,
            entityType,
            entityId,
            details is not null ? JsonSerializer.Serialize(details) : null);

        db.AuditLog.Add(entry);
        await db.SaveChangesAsync(ct);
    }
}
