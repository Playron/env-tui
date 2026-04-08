using ContactExtractor.Api.Auth;
using ContactExtractor.Api.Services.Integrations;

namespace ContactExtractor.Api.Endpoints;

public static class IntegrationEndpoints
{
    public static void MapIntegrationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/integrations")
            .WithTags("Integrations");

        group.MapGet("/", GetAvailableIntegrations)
            .Produces<List<string>>(200)
            .WithSummary("Hent tilgjengelige CRM-integrasjoner");

        group.MapPost("/hubspot/export/{sessionId:guid}", ExportToHubSpot)
            .Produces<CrmExportResult>(200)
            .Produces(404)
            .Produces<string>(400)
            .WithSummary("Eksporter kontakter til HubSpot");

        group.MapPost("/google/export/{sessionId:guid}", ExportToGoogle)
            .Produces<CrmExportResult>(200)
            .Produces(404)
            .Produces<string>(400)
            .WithSummary("Eksporter kontakter til Google Contacts");
    }

    private static Ok<List<string>> GetAvailableIntegrations() =>
        TypedResults.Ok<List<string>>(["HubSpot", "Google Contacts"]);

    private static async Task<Results<Ok<CrmExportResult>, NotFound, BadRequest<string>>> ExportToHubSpot(
        Guid sessionId,
        string apiKey,
        AppDbContext db,
        HubSpotExporter exporter,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            return TypedResults.BadRequest("API-nøkkel er påkrevd.");

        var session = await db.UploadSessions
            .AsNoTracking()
            .Include(s => s.Contacts)
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct);

        if (session is null) return TypedResults.NotFound();

        var result = await exporter.ExportAsync(session.Contacts.ToList(), apiKey, ct);
        return TypedResults.Ok(result);
    }

    private static async Task<Results<Ok<CrmExportResult>, NotFound, BadRequest<string>>> ExportToGoogle(
        Guid sessionId,
        string accessToken,
        AppDbContext db,
        GoogleContactsExporter exporter,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
            return TypedResults.BadRequest("Access token er påkrevd.");

        var session = await db.UploadSessions
            .AsNoTracking()
            .Include(s => s.Contacts)
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct);

        if (session is null) return TypedResults.NotFound();

        var result = await exporter.ExportAsync(session.Contacts.ToList(), accessToken, ct);
        return TypedResults.Ok(result);
    }
}
