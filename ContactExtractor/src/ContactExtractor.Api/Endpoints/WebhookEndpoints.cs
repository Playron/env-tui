namespace ContactExtractor.Api.Endpoints;

public static class WebhookEndpoints
{
    public static void MapWebhookEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/webhooks")
            .WithTags("Webhooks");

        group.MapGet("/", GetWebhooks)
            .Produces<List<WebhookConfigDto>>(200)
            .WithSummary("Hent alle webhooks for brukeren");

        group.MapPost("/", CreateWebhook)
            .Produces<WebhookConfigDto>(201)
            .Produces<string>(400)
            .WithSummary("Registrer ny webhook");

        group.MapDelete("/{webhookId:guid}", DeleteWebhook)
            .Produces(204)
            .Produces(404)
            .WithSummary("Slett en webhook");

        group.MapPost("/{webhookId:guid}/test", TestWebhook)
            .Produces(200)
            .Produces(404)
            .WithSummary("Send test-payload til webhook");
    }

    private static async Task<Ok<List<WebhookConfigDto>>> GetWebhooks(
        AppDbContext db,
        CurrentUserService currentUser,
        CancellationToken ct)
    {
        var userId = currentUser.UserIdOrAnonymous;
        var webhooks = await db.Webhooks
            .AsNoTracking()
            .Where(w => w.UserId == userId)
            .OrderByDescending(w => w.CreatedAt)
            .Select(w => new WebhookConfigDto(w.Id, w.Url, w.Event, w.IsActive, w.CreatedAt))
            .ToListAsync(ct);

        return TypedResults.Ok(webhooks);
    }

    private static async Task<Results<Created<WebhookConfigDto>, BadRequest<string>>> CreateWebhook(
        CreateWebhookDto dto,
        AppDbContext db,
        CurrentUserService currentUser,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Url))
            return TypedResults.BadRequest("URL er påkrevd.");

        if (!Uri.TryCreate(dto.Url, UriKind.Absolute, out _))
            return TypedResults.BadRequest("Ugyldig URL.");

        var userId = currentUser.UserIdOrAnonymous;
        var webhook = new WebhookConfig(userId, dto.Url, dto.Event, dto.Secret);
        db.Webhooks.Add(webhook);
        await db.SaveChangesAsync(ct);

        var result = new WebhookConfigDto(webhook.Id, webhook.Url, webhook.Event, webhook.IsActive, webhook.CreatedAt);
        return TypedResults.Created($"/api/webhooks/{webhook.Id}", result);
    }

    private static async Task<Results<NoContent, NotFound>> DeleteWebhook(
        Guid webhookId,
        AppDbContext db,
        CurrentUserService currentUser,
        CancellationToken ct)
    {
        var userId = currentUser.UserIdOrAnonymous;
        var webhook = await db.Webhooks
            .FirstOrDefaultAsync(w => w.Id == webhookId && w.UserId == userId, ct);

        if (webhook is null) return TypedResults.NotFound();

        db.Webhooks.Remove(webhook);
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok, NotFound>> TestWebhook(
        Guid webhookId,
        AppDbContext db,
        WebhookService webhookService,
        CurrentUserService currentUser,
        CancellationToken ct)
    {
        var userId = currentUser.UserIdOrAnonymous;
        var webhook = await db.Webhooks
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == webhookId && w.UserId == userId, ct);

        if (webhook is null) return TypedResults.NotFound();

        await webhookService.DispatchAsync(userId, webhook.Event,
            new { test = true, webhookId, timestamp = DateTime.UtcNow }, ct);

        return TypedResults.Ok();
    }
}
