namespace ContactExtractor.Api.Domain;

public class WebhookConfig
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public string UserId { get; private set; } = "anonymous";
    public string Url { get; private set; } = string.Empty;
    public string Event { get; private set; } = string.Empty;  // "extraction.completed", "duplicates.found"
    public string? Secret { get; private set; }                // HMAC-signering
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    private WebhookConfig() { } // EF Core

    public WebhookConfig(string userId, string url, string @event, string? secret = null)
    {
        Id = Guid.CreateVersion7();
        UserId = userId;
        Url = url;
        Event = @event;
        Secret = secret;
    }
}
