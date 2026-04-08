namespace ContactExtractor.Api.Contracts;

public record AuditLogDto(
    Guid Id,
    string UserId,
    string Action,
    string EntityType,
    Guid? EntityId,
    string? Details,
    DateTime Timestamp);
