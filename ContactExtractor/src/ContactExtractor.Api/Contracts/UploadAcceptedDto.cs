namespace ContactExtractor.Api.Contracts;

public record UploadAcceptedDto(
    Guid SessionId,
    string StreamUrl,    // SSE-endepunkt: /api/upload/{sessionId}/stream
    string ResultUrl);   // Polling-endepunkt: /api/upload/{sessionId}/result

public record ExtractionStatusDto(
    string Status,
    string Message);
