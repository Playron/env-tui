namespace ContactExtractor.Api.Contracts;

public record WebhookConfigDto(
    Guid Id,
    string Url,
    string Event,
    bool IsActive,
    DateTime CreatedAt);

public record CreateWebhookDto(
    string Url,
    string Event,
    string? Secret = null);
