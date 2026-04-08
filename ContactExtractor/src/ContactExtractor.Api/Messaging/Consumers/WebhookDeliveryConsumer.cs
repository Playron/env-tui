using ContactExtractor.Api.Messaging.Messages;
using MassTransit;

namespace ContactExtractor.Api.Messaging.Consumers;

public class WebhookDeliveryConsumer(
    IHttpClientFactory httpClientFactory,
    ILogger<WebhookDeliveryConsumer> logger) : IConsumer<WebhookDeliveryRequested>
{
    private const int MaxAttempts = 3;

    public async Task Consume(ConsumeContext<WebhookDeliveryRequested> context)
    {
        var msg = context.Message;
        var ct  = context.CancellationToken;

        try
        {
            using var httpClient = httpClientFactory.CreateClient("webhook");
            using var request = new HttpRequestMessage(HttpMethod.Post, msg.Url);
            request.Content = new StringContent(msg.Payload, System.Text.Encoding.UTF8, "application/json");

            if (!string.IsNullOrWhiteSpace(msg.Secret))
            {
                var sig = WebhookService.ComputeSignature(msg.Secret, msg.Payload);
                request.Headers.Add("X-Signature", sig);
            }

            request.Headers.Add("X-Event",   msg.Event);
            request.Headers.Add("X-Attempt", msg.AttemptNumber.ToString());

            var response = await httpClient.SendAsync(request, ct);

            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation(
                    "Webhook levert til {Url} – event: {Event}", msg.Url, msg.Event);
            }
            else
            {
                logger.LogWarning(
                    "Webhook feilet for {Url}: HTTP {Status}", msg.Url, (int)response.StatusCode);
                await RetryIfNeeded(context, msg, ct);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Webhook levering feilet for {Url}", msg.Url);
            await RetryIfNeeded(context, msg, ct);
        }
    }

    private static async Task RetryIfNeeded(
        ConsumeContext context,
        WebhookDeliveryRequested msg,
        CancellationToken ct)
    {
        if (msg.AttemptNumber >= MaxAttempts) return;

        // Exponential backoff: 2s, 4s, 8s
        var delay = TimeSpan.FromSeconds(Math.Pow(2, msg.AttemptNumber));
        await Task.Delay(delay, ct);

        await context.Publish(msg with { AttemptNumber = msg.AttemptNumber + 1 }, ct);
    }
}
