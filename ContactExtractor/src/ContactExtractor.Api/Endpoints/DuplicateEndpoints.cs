namespace ContactExtractor.Api.Endpoints;

public static class DuplicateEndpoints
{
    public static void MapDuplicateEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/duplicates")
            .WithTags("Duplicates");

        group.MapGet("/", GetDuplicateGroups)
            .Produces<List<DuplicateGroupDto>>(200)
            .WithSummary("Hent alle uløste duplikatgrupper for brukeren");

        group.MapPost("/{groupId:guid}/merge", MergeContacts)
            .Produces<ContactDto>(200)
            .Produces(404)
            .Produces<string>(400)
            .WithSummary("Slå sammen kontakter i en gruppe til én primærkontakt");

        group.MapPost("/{groupId:guid}/dismiss", DismissGroup)
            .Produces(204)
            .Produces(404)
            .WithSummary("Merk duplikatgruppe som løst (ikke-duplikater)");
    }

    private static async Task<Ok<List<DuplicateGroupDto>>> GetDuplicateGroups(
        AppDbContext db,
        CurrentUserService currentUser,
        CancellationToken ct)
    {
        var userId = currentUser.UserIdOrAnonymous;
        var groups = await db.DuplicateGroups
            .AsNoTracking()
            .Include(g => g.Contacts)
                .ThenInclude(c => c.Tags)
            .Where(g => g.UserId == userId && !g.Resolved)
            .OrderByDescending(g => g.Similarity)
            .ToListAsync(ct);

        var dtos = groups.Select(g => new DuplicateGroupDto(
            g.Id,
            g.Similarity,
            g.Resolved,
            g.Contacts.Select(c => c.ToDto()).ToList()))
            .ToList();

        return TypedResults.Ok(dtos);
    }

    private static async Task<Results<Ok<ContactDto>, NotFound, BadRequest<string>>> MergeContacts(
        Guid groupId,
        MergeContactsDto dto,
        AppDbContext db,
        CurrentUserService currentUser,
        CancellationToken ct)
    {
        var userId = currentUser.UserIdOrAnonymous;
        var group = await db.DuplicateGroups
            .Include(g => g.Contacts)
            .FirstOrDefaultAsync(g => g.Id == groupId && g.UserId == userId, ct);

        if (group is null) return TypedResults.NotFound();

        var primary = group.Contacts.FirstOrDefault(c => c.Id == dto.PrimaryContactId);
        if (primary is null)
            return TypedResults.BadRequest("Primærkontakt finnes ikke i gruppen.");

        // Fyll inn manglende felt fra de andre kontaktene
        foreach (var other in group.Contacts.Where(c => c.Id != dto.PrimaryContactId))
        {
            primary.FirstName    ??= other.FirstName;
            primary.LastName     ??= other.LastName;
            primary.FullName     ??= other.FullName;
            primary.Email        ??= other.Email;
            primary.Phone        ??= other.Phone;
            primary.Organization ??= other.Organization;
            primary.Title        ??= other.Title;
            primary.Address      ??= other.Address;
        }

        // Påtving override-felt fra request
        if (dto.OverrideFields is not null)
        {
            if (dto.OverrideFields.FirstName is not null)    primary.FirstName    = dto.OverrideFields.FirstName;
            if (dto.OverrideFields.LastName is not null)     primary.LastName     = dto.OverrideFields.LastName;
            if (dto.OverrideFields.FullName is not null)     primary.FullName     = dto.OverrideFields.FullName;
            if (dto.OverrideFields.Organization is not null) primary.Organization = dto.OverrideFields.Organization;
            if (dto.OverrideFields.Title is not null)        primary.Title        = dto.OverrideFields.Title;
            if (dto.OverrideFields.Address is not null)      primary.Address      = dto.OverrideFields.Address;
            if (dto.OverrideFields.Email is not null)        primary.Email        = dto.OverrideFields.Email;
            if (dto.OverrideFields.Phone is not null)        primary.Phone        = dto.OverrideFields.Phone;
        }

        // Slett duplikatene, behold primærkontakt
        var toDelete = group.Contacts.Where(c => c.Id != primary.Id).ToList();
        db.Contacts.RemoveRange(toDelete);

        // Fjern duplikatgruppetilknytning fra primærkontakt
        primary.DuplicateGroupId = null;

        group.Resolve();
        await db.SaveChangesAsync(ct);

        return TypedResults.Ok(primary.ToDto());
    }

    private static async Task<Results<NoContent, NotFound>> DismissGroup(
        Guid groupId,
        AppDbContext db,
        CurrentUserService currentUser,
        CancellationToken ct)
    {
        var userId = currentUser.UserIdOrAnonymous;
        var group = await db.DuplicateGroups
            .Include(g => g.Contacts)
            .FirstOrDefaultAsync(g => g.Id == groupId && g.UserId == userId, ct);

        if (group is null) return TypedResults.NotFound();

        // Nullstill DuplicateGroupId på alle kontakter i gruppen
        foreach (var contact in group.Contacts)
            contact.DuplicateGroupId = null;

        group.Resolve();
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }
}
