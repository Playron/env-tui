using ContactExtractor.Api.Auth;

namespace ContactExtractor.Api.Endpoints;

public static class TagEndpoints
{
    public static void MapTagEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/tags")
            .WithTags("Tags");

        group.MapGet("/", GetAllTags)
            .Produces<List<TagDto>>(200)
            .WithSummary("Hent alle tags for brukeren");

        group.MapPost("/", CreateTag)
            .Produces<TagDto>(201)
            .Produces<string>(400)
            .WithSummary("Opprett en ny tag");

        group.MapPut("/{tagId:guid}", UpdateTag)
            .Produces<TagDto>(200)
            .Produces(404)
            .WithSummary("Oppdater en tag");

        group.MapDelete("/{tagId:guid}", DeleteTag)
            .Produces(204)
            .Produces(404)
            .WithSummary("Slett en tag");

        group.MapPost("/contacts/add", AddTagToContacts)
            .Produces(204)
            .Produces(404)
            .WithSummary("Legg tag til kontakter (bulk)");

        group.MapPost("/contacts/remove", RemoveTagFromContacts)
            .Produces(204)
            .WithSummary("Fjern tag fra kontakter (bulk)");
    }

    private static async Task<Ok<List<TagDto>>> GetAllTags(
        AppDbContext db,
        CurrentUserService currentUser,
        CancellationToken ct)
    {
        var userId = currentUser.UserIdOrAnonymous;
        var tags = await db.Tags
            .AsNoTracking()
            .Where(t => t.UserId == userId)
            .OrderBy(t => t.Name)
            .Select(t => new TagDto(t.Id, t.Name, t.Color))
            .ToListAsync(ct);

        return TypedResults.Ok(tags);
    }

    private static async Task<Results<Created<TagDto>, BadRequest<string>>> CreateTag(
        CreateTagDto dto,
        AppDbContext db,
        CurrentUserService currentUser,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return TypedResults.BadRequest("Tag-navn er påkrevd.");

        var userId = currentUser.UserIdOrAnonymous;
        var tag = new Tag(dto.Name.Trim(), dto.Color, userId: userId);
        db.Tags.Add(tag);
        await db.SaveChangesAsync(ct);

        var result = new TagDto(tag.Id, tag.Name, tag.Color);
        return TypedResults.Created($"/api/tags/{tag.Id}", result);
    }

    private static async Task<Results<Ok<TagDto>, NotFound>> UpdateTag(
        Guid tagId,
        UpdateTagDto dto,
        AppDbContext db,
        CurrentUserService currentUser,
        CancellationToken ct)
    {
        var userId = currentUser.UserIdOrAnonymous;
        var tag = await db.Tags
            .FirstOrDefaultAsync(t => t.Id == tagId && t.UserId == userId, ct);

        if (tag is null) return TypedResults.NotFound();

        if (!string.IsNullOrWhiteSpace(dto.Name)) tag.Name  = dto.Name.Trim();
        if (dto.Color is not null)                tag.Color = dto.Color;

        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(new TagDto(tag.Id, tag.Name, tag.Color));
    }

    private static async Task<Results<NoContent, NotFound>> DeleteTag(
        Guid tagId,
        AppDbContext db,
        CurrentUserService currentUser,
        CancellationToken ct)
    {
        var userId = currentUser.UserIdOrAnonymous;
        var tag = await db.Tags
            .FirstOrDefaultAsync(t => t.Id == tagId && t.UserId == userId, ct);

        if (tag is null) return TypedResults.NotFound();

        db.Tags.Remove(tag);
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    private static async Task<Results<NoContent, NotFound>> AddTagToContacts(
        BulkTagDto dto,
        AppDbContext db,
        CurrentUserService currentUser,
        CancellationToken ct)
    {
        var userId = currentUser.UserIdOrAnonymous;
        var tag = await db.Tags
            .FirstOrDefaultAsync(t => t.Id == dto.TagId && t.UserId == userId, ct);

        if (tag is null) return TypedResults.NotFound();

        var contacts = await db.Contacts
            .Include(c => c.Tags)
            .Where(c => dto.ContactIds.Contains(c.Id))
            .ToListAsync(ct);

        foreach (var contact in contacts)
            contact.AddTag(tag);

        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    private static async Task<NoContent> RemoveTagFromContacts(
        BulkTagDto dto,
        AppDbContext db,
        CancellationToken ct)
    {
        var contacts = await db.Contacts
            .Include(c => c.Tags)
            .Where(c => dto.ContactIds.Contains(c.Id))
            .ToListAsync(ct);

        foreach (var contact in contacts)
            contact.RemoveTag(dto.TagId);

        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }
}
