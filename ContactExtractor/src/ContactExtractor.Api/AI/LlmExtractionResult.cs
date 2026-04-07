namespace ContactExtractor.Api.AI;

public record LlmExtractionResult(
    List<LlmContact> Contacts,
    string? Reasoning,
    double OverallConfidence);

public record LlmContact(
    string? FirstName,
    string? LastName,
    string? FullName,
    string? Email,
    string? Phone,
    string? Organization,
    string? Title,
    string? Address);
