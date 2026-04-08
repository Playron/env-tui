namespace ContactExtractor.Api.Messaging.Messages;

public record WebhookDeliveryRequested(
    Guid WebhookId,
    string Url,
    string Event,
    string? Secret,
    string Payload,
    int AttemptNumber = 1);
