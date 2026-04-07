namespace ContactExtractor.Api.Endpoints;

public static class ContactEndpoints
{
    public static void MapContactEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/contacts")
            .WithTags("Contacts");

        group.MapGet("/", GetAllSessions)
            .Produces<List<SessionSummaryDto>>(200)
            .WithSummary("Hent alle opplastingssesjoner");

        group.MapGet("/{sessionId:guid}", GetContactsBySession)
            .Produces<ExtractionResultDto>(200)
            .Produces(404)
            .WithSummary("Hent kontakter for en sesjon");

        group.MapPut("/{sessionId:guid}/mapping", RemapContacts)
            .Produces<ExtractionResultDto>(200)
            .Produces(404)
            .Produces<string>(400)
            .WithSummary("Oppdater kolonne-mapping og re-ekstraher");

        group.MapPut("/{sessionId:guid}/contacts/{contactId:guid}", UpdateContact)
            .Produces<ContactDto>(200)
            .Produces(404)
            .WithSummary("Oppdater en enkelt kontakt");

        group.MapDelete("/{sessionId:guid}", DeleteSession)
            .Produces(204)
            .Produces(404)
            .WithSummary("Slett en hel sesjon og alle tilhørende kontakter");
    }

    private static async Task<Ok<List<SessionSummaryDto>>> GetAllSessions(
        AppDbContext db,
        CancellationToken ct)
    {
        var sessions = await db.UploadSessions
            .AsNoTracking()
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new SessionSummaryDto(
                s.Id,
                s.OriginalFileName,
                s.FileType,
                s.TotalRowsProcessed,
                s.Contacts.Count,
                s.UsedAi,
                s.CreatedAt))
            .ToListAsync(ct);

        return TypedResults.Ok(sessions);
    }

    private static async Task<Results<Ok<ExtractionResultDto>, NotFound>> GetContactsBySession(
        Guid sessionId,
        AppDbContext db,
        CancellationToken ct)
    {
        var session = await db.UploadSessions
            .AsNoTracking()
            .Include(s => s.Contacts)
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct);

        if (session is null) return TypedResults.NotFound();

        return TypedResults.Ok(session.ToDto());
    }

    private static async Task<Results<Ok<ExtractionResultDto>, NotFound, BadRequest<string>>> RemapContacts(
        Guid sessionId,
        ColumnMappingUpdateDto dto,
        AppDbContext db,
        CancellationToken ct)
    {
        var session = await db.UploadSessions
            .Include(s => s.Contacts)
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct);

        if (session is null) return TypedResults.NotFound();
        if (dto.Mappings.Count is 0)
            return TypedResults.BadRequest("Ingen mappinger angitt.");

        // Remove existing contacts and re-extract would require original file;
        // instead return current contacts with updated mapping info as warning
        return TypedResults.Ok(session.ToDto(["Re-mapping krever ny opplasting av originalfilen."]));
    }

    private static async Task<Results<Ok<ContactDto>, NotFound>> UpdateContact(
        Guid sessionId,
        Guid contactId,
        ContactUpdateDto dto,
        AppDbContext db,
        CancellationToken ct)
    {
        var contact = await db.Contacts
            .FirstOrDefaultAsync(c => c.Id == contactId && c.UploadSessionId == sessionId, ct);

        if (contact is null) return TypedResults.NotFound();

        contact.FirstName    = dto.FirstName    ?? contact.FirstName;
        contact.LastName     = dto.LastName     ?? contact.LastName;
        contact.FullName     = dto.FullName     ?? contact.FullName;
        contact.Organization = dto.Organization ?? contact.Organization;
        contact.Title        = dto.Title        ?? contact.Title;
        contact.Address      = dto.Address      ?? contact.Address;

        if (dto.Email is not null) contact.SetEmail(EmailAddress.TryCreate(dto.Email));
        if (dto.Phone is not null) contact.SetPhone(PhoneNumber.TryCreate(dto.Phone));

        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(contact.ToDto());
    }

    private static async Task<Results<NoContent, NotFound>> DeleteSession(
        Guid sessionId,
        AppDbContext db,
        CancellationToken ct)
    {
        var session = await db.UploadSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct);

        if (session is null) return TypedResults.NotFound();

        db.UploadSessions.Remove(session);
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }
}

public record SessionSummaryDto(
    Guid Id,
    string OriginalFileName,
    string FileType,
    int TotalRowsProcessed,
    int ContactCount,
    bool UsedAi,
    DateTime CreatedAt);
