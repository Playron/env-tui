using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ContactExtractor.Api.Messaging.Messages;
using MassTransit;

namespace ContactExtractor.Api.Services;

public class WebhookService(AppDbContext db, IPublishEndpoint bus)
{
    /// <summary>Dispatcherer webhook-events for alle aktive webhooks med matchende event-navn.</summary>
    public async Task DispatchAsync(
        string userId,
        string eventName,
        object payload,
        CancellationToken ct = default)
    {
        var webhooks = await db.Webhooks
            .AsNoTracking()
            .Where(w => w.UserId == userId && w.IsActive && w.Event == eventName)
            .ToListAsync(ct);

        if (webhooks.Count == 0) return;

        var json = JsonSerializer.Serialize(new
        {
            @event = eventName,
            timestamp = DateTime.UtcNow,
            data = payload
        });

        foreach (var webhook in webhooks)
        {
            await bus.Publish(new WebhookDeliveryRequested(
                webhook.Id, webhook.Url, webhook.Event, webhook.Secret, json), ct);
        }
    }

    /// <summary>Beregner HMAC-SHA256 signatur for webhook-payload.</summary>
    public static string ComputeSignature(string secret, string payload)
    {
        var key  = Encoding.UTF8.GetBytes(secret);
        var data = Encoding.UTF8.GetBytes(payload);
        var hash = HMACSHA256.HashData(key, data);
        return $"sha256={Convert.ToHexString(hash).ToLowerInvariant()}";
    }
}
