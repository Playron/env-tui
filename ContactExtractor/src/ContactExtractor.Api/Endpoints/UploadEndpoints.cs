using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using ContactExtractor.Api.Messaging.Messages;
using MassTransit;

namespace ContactExtractor.Api.Endpoints;

public static class UploadEndpoints
{
    public static void MapUploadEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/upload")
            .WithTags("Upload")
            .DisableAntiforgery();

        // 1) Asynkron opplasting – returnerer 202 Accepted med sessionId og stream-URL
        group.MapPost("/", HandleUpload)
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<UploadAcceptedDto>(202)
            .Produces<string>(400)
            .WithSummary("Last opp fil for asynkron ekstraksjon – returnerer 202 Accepted");

        // 2) SSE-stream for fremdrift
        group.MapGet("/{sessionId:guid}/stream", HandleStream)
            .WithSummary("SSE-stream for fremdriftshendelser under ekstraksjon");

        // 3) Polling-fallback – hent ferdig resultat fra DB
        group.MapGet("/{sessionId:guid}/result", HandleResult)
            .Produces<ExtractionResultDto>(200)
            .Produces<ExtractionStatusDto>(202)
            .Produces(404)
            .WithSummary("Hent ekstrasjonsresultat (polling-fallback)");

        // 4) Forhåndsvisning av fil og kolonne-mapping-forslag (uendret)
        group.MapPost("/preview", HandlePreview)
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<PreviewResultDto>(200)
            .Produces<string>(400)
            .WithSummary("Forhåndsvisning av fil og kolonne-mapping-forslag");

        // 5) Støttede formater (uendret)
        group.MapGet("/supported-formats", GetSupportedFormats)
            .Produces<SupportedFormatDto[]>(200)
            .WithSummary("Hent støttede filformater");
    }

    // ── POST /api/upload ──────────────────────────────────────────────────────
    // Lagrer fil midlertidig, oppretter sesjon i DB, publiserer melding til kø.
    private static async Task<Results<Accepted<UploadAcceptedDto>, BadRequest<string>>> HandleUpload(
        IFormFile file,
        AppDbContext db,
        FileParserFactory parserFactory,
        IPublishEndpoint publishEndpoint,
        CancellationToken ct)
    {
        if (file.Length is 0)
            return TypedResults.BadRequest("Filen er tom.");

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

        if (parserFactory.GetParser(extension) is null)
            return TypedResults.BadRequest($"Filtypen '{extension}' støttes ikke.");

        // Lagre fil midlertidig på disk
        var tempDir = Path.Combine(Path.GetTempPath(), "contact-extractor");
        Directory.CreateDirectory(tempDir);
        var tempPath = Path.Combine(tempDir, $"{Guid.NewGuid()}{extension}");

        await using (var fs = File.Create(tempPath))
            await file.CopyToAsync(fs, ct);

        // Opprett sesjon i DB med status Pending
        var session = new UploadSession(file.FileName, extension, 0);
        db.UploadSessions.Add(session);
        await db.SaveChangesAsync(ct);

        // Publiser til MassTransit (InMemory eller RabbitMQ)
        await publishEndpoint.Publish(new ExtractionRequested(
            session.Id, tempPath, file.FileName, extension), ct);

        var streamUrl = $"/api/upload/{session.Id}/stream";
        var resultUrl = $"/api/upload/{session.Id}/result";

        return TypedResults.Accepted(resultUrl,
            new UploadAcceptedDto(session.Id, streamUrl, resultUrl));
    }

    // ── GET /api/upload/{sessionId}/stream ───────────────────────────────────
    // SSE-endepunkt. Bruker TypedResults.ServerSentEvents fra .NET 10 – håndterer
    // headers, framing (data: ...\n\n) og flushing automatisk.
    // Håndterer sen tilkobling: lever terminal-event umiddelbart hvis allerede ferdig.
    private static async Task<IResult> HandleStream(
        Guid sessionId,
        SseProgressService progressService,
        AppDbContext db,
        CancellationToken ct)
    {
        if (progressService.Exists(sessionId))
            return TypedResults.ServerSentEvents(progressService.StreamAsync(sessionId, ct));

        // Sesjonen er ikke i minnet – sen klient, sjekk DB
        var session = await db.UploadSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct);

        if (session is null) return TypedResults.NotFound();

        if (session.Status is ExtractionStatus.Completed or ExtractionStatus.Failed)
        {
            var stage = session.Status == ExtractionStatus.Completed ? "done" : "failed";
            var msg = session.Status == ExtractionStatus.Completed
                ? $"Ferdig! {session.TotalRowsProcessed} kontakter ekstrahert."
                : $"Feil: {session.ErrorMessage}";
            var final = new SseProgressEvent(sessionId, stage, msg, session.TotalRowsProcessed, null);
            return TypedResults.ServerSentEvents(SingleEventAsync(final));
        }

        return TypedResults.NotFound();
    }

    private static async IAsyncEnumerable<SseItem<SseProgressEvent>> SingleEventAsync(
        SseProgressEvent evt,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        yield return new SseItem<SseProgressEvent>(evt, evt.Stage);
        await Task.CompletedTask;
    }

    // ── GET /api/upload/{sessionId}/result ───────────────────────────────────
    // Polling-fallback: 202 mens pågår, 200 når ferdig, 404 hvis ukjent.
    private static async Task<Results<Ok<ExtractionResultDto>, Accepted<ExtractionStatusDto>, NotFound>>
        HandleResult(
            Guid sessionId,
            AppDbContext db,
            CancellationToken ct)
    {
        var session = await db.UploadSessions
            .AsNoTracking()
            .Include(s => s.Contacts)
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct);

        if (session is null)
            return TypedResults.NotFound();

        if (session.Status is ExtractionStatus.Completed or ExtractionStatus.Failed)
            return TypedResults.Ok(session.ToDto());

        return TypedResults.Accepted(
            $"/api/upload/{sessionId}/result",
            new ExtractionStatusDto(session.Status.ToString(), "Ekstraksjon pågår..."));
    }

    // ── POST /api/upload/preview ──────────────────────────────────────────────
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

    // ── GET /api/upload/supported-formats ────────────────────────────────────
    private static Ok<SupportedFormatDto[]> GetSupportedFormats() =>
        TypedResults.Ok<SupportedFormatDto[]>(
        [
            new(".csv",  "Kommaseparert fil",        "📄"),
            new(".xlsx", "Excel-regneark",            "📊"),
            new(".pdf",  "PDF-dokument (AI-støttet)", "📕"),
            new(".docx", "Word-dokument (AI-støttet)","📝"),
            new(".txt",  "Tekstfil (AI-støttet)",     "📃"),
            new(".vcf",  "vCard-kontaktfil",          "👤")
        ]);
}
