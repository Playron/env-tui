namespace ContactExtractor.Api.Endpoints;

public static class UploadEndpoints
{
    public static void MapUploadEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/upload")
            .WithTags("Upload")
            .DisableAntiforgery();

        group.MapPost("/", HandleUpload)
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<ExtractionResultDto>(200)
            .Produces<string>(400)
            .WithSummary("Last opp fil og ekstraher kontakter (AI brukes automatisk ved behov)");

        group.MapPost("/preview", HandlePreview)
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<PreviewResultDto>(200)
            .Produces<string>(400)
            .WithSummary("Forhåndsvisning av fil og kolonne-mapping-forslag");

        group.MapGet("/supported-formats", GetSupportedFormats)
            .Produces<SupportedFormatDto[]>(200)
            .WithSummary("Hent støttede filformater");
    }

    private static async Task<Results<Ok<ExtractionResultDto>, BadRequest<string>>> HandleUpload(
        IFormFile file,
        FileParserFactory parserFactory,
        AppDbContext db,
        CancellationToken ct)
    {
        if (file.Length is 0)
            return TypedResults.BadRequest("Filen er tom.");

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var parser = parserFactory.GetParser(extension);

        if (parser is null)
            return TypedResults.BadRequest($"Filtypen '{extension}' støttes ikke.");

        await using var stream = file.OpenReadStream();
        List<Contact> contacts;

        try
        {
            contacts = await parser.ParseAsync(stream, file.FileName, ct);
        }
        catch (Exception ex)
        {
            return TypedResults.BadRequest($"Kunne ikke tolke filen: {ex.Message}");
        }

        var usedAi = contacts.Any(c => c.ExtractionSource == "ai");
        var session = new UploadSession(file.FileName, extension, contacts.Count, usedAi);
        session.AddContacts(contacts);
        db.UploadSessions.Add(session);
        await db.SaveChangesAsync(ct);

        var warnings = new List<string>();
        if (usedAi)
            warnings.Add("AI ble brukt til å ekstrahere noen kontakter. Vennligst verifiser resultatene.");

        return TypedResults.Ok(session.ToDto(warnings));
    }

    private static async Task<Results<Ok<PreviewResultDto>, BadRequest<string>>> HandlePreview(
        IFormFile file,
        FileParserFactory parserFactory,
        CancellationToken ct)
    {
        if (file.Length is 0)
            return TypedResults.BadRequest("Filen er tom.");

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var parser = parserFactory.GetParser(extension);

        if (parser is null)
            return TypedResults.BadRequest($"Filtypen '{extension}' støttes ikke.");

        await using var stream = file.OpenReadStream();

        try
        {
            var preview = await parser.PreviewAsync(stream, file.FileName, ct);
            return TypedResults.Ok(preview);
        }
        catch (Exception ex)
        {
            return TypedResults.BadRequest($"Kunne ikke lese filen: {ex.Message}");
        }
    }

    private static Ok<SupportedFormatDto[]> GetSupportedFormats() =>
        TypedResults.Ok<SupportedFormatDto[]>(
        [
            new(".csv",  "Kommaseparert fil",       "📄"),
            new(".xlsx", "Excel-regneark",           "📊"),
            new(".pdf",  "PDF-dokument (AI-støttet)","📕"),
            new(".docx", "Word-dokument (AI-støttet)","📝"),
            new(".txt",  "Tekstfil (AI-støttet)",    "📃"),
            new(".vcf",  "vCard-kontaktfil",         "👤")
        ]);
}
