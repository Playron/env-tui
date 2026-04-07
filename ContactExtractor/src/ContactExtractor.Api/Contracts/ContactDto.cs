namespace ContactExtractor.Api.Contracts;

public record ContactDto(
    Guid Id,
    string? FirstName,
    string? LastName,
    string? FullName,
    string? Email,
    string? Phone,
    string? Organization,
    string? Title,
    string? Address,
    double Confidence,
    string ExtractionSource);   // "regex" | "ai" | "manual"

public record ContactUpdateDto(
    string? FirstName,
    string? LastName,
    string? FullName,
    string? Email,
    string? Phone,
    string? Organization,
    string? Title,
    string? Address);
