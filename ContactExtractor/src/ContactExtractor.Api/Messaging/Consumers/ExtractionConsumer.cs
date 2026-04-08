using ContactExtractor.Api.Messaging.Messages;
using MassTransit;

namespace ContactExtractor.Api.Messaging.Consumers;

public class ExtractionConsumer(
    IServiceScopeFactory scopeFactory,
    SseProgressService progressService,
    ILogger<ExtractionConsumer> logger) : IConsumer<ExtractionRequested>
{
    public async Task Consume(ConsumeContext<ExtractionRequested> context)
    {
        var msg = context.Message;
        var ct = context.CancellationToken;

        progressService.Create(msg.SessionId);

        try
        {
            await RunExtractionAsync(msg, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ekstraksjon feilet for sesjon {SessionId}", msg.SessionId);
            progressService.Complete(msg.SessionId, Progress(msg, "failed", $"Feil: {ex.Message}"));
            await SaveFailureStatusAsync(msg.SessionId, ex.Message, ct);
        }
        finally
        {
            DeleteTempFile(msg.FilePath);
            ScheduleSseCleanup(msg.SessionId);
        }
    }

    private async Task RunExtractionAsync(ExtractionRequested msg, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var parserFactory = scope.ServiceProvider.GetRequiredService<FileParserFactory>();
        var extractionService = scope.ServiceProvider.GetRequiredService<ContactExtractionService>();
        var llmService = scope.ServiceProvider.GetRequiredService<ILlmService>();
        var llmSettings = scope.ServiceProvider.GetRequiredService<IOptions<LlmSettings>>().Value;

        var session = await db.UploadSessions.FindAsync([msg.SessionId], ct);
        if (session is null)
        {
            logger.LogWarning("Sesjon {SessionId} finnes ikke i DB – hopper over", msg.SessionId);
            return;
        }

        // Steg 1: Regex-ekstraksjon
        session.UpdateStatus(ExtractionStatus.Extracting);
        await db.SaveChangesAsync(ct);
        progressService.Publish(msg.SessionId, Progress(msg, "extracting", "Leser fil og kjører regex-ekstraksjon...", progress: 0.1));

        var (regexContacts, rawText) = await ExtractWithRegexAsync(msg, parserFactory, extractionService, ct);

        progressService.Publish(msg.SessionId, Progress(msg, "regex_done",
            $"Regex fant {regexContacts.Count} kontakter.", regexContacts.Count, 0.4));

        // Steg 2: AI-ekstraksjon (hvis nødvendig)
        var (finalContacts, usedAi) = await TryAiExtractionAsync(
            msg, session, db, llmService, llmSettings, regexContacts, rawText, ct);

        // Steg 3: Lagre resultat
        session.AddContacts(finalContacts);
        session.SetRowsProcessed(finalContacts.Count);
        session.SetUsedAi(usedAi);
        session.UpdateStatus(ExtractionStatus.Completed);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Sesjon {SessionId} fullført: {Count} kontakter, AI brukt: {UsedAi}",
            msg.SessionId, finalContacts.Count, usedAi);

        progressService.Complete(msg.SessionId, Progress(msg, "done",
            $"Ferdig! {finalContacts.Count} kontakter ekstrahert.", finalContacts.Count, 1.0));
    }

    private static async Task<(List<Contact> Contacts, string RawText)> ExtractWithRegexAsync(
        ExtractionRequested msg, FileParserFactory parserFactory,
        ContactExtractionService extractionService, CancellationToken ct)
    {
        var parser = parserFactory.GetParser(msg.FileExtension);
        if (parser is not null)
        {
            await using var fs = File.OpenRead(msg.FilePath);
            return await parser.ParseWithoutAiAsync(fs, msg.FileName, ct);
        }

        var rawText = await File.ReadAllTextAsync(msg.FilePath, ct);
        return (extractionService.ExtractFromText(rawText, msg.SessionId), rawText);
    }

    private async Task<(List<Contact> Contacts, bool UsedAi)> TryAiExtractionAsync(
        ExtractionRequested msg, UploadSession session, AppDbContext db,
        ILlmService llmService, LlmSettings llmSettings,
        List<Contact> regexContacts, string rawText, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rawText) || !ContactMergeHelper.ShouldUseAi(regexContacts))
            return (regexContacts, false);

        session.UpdateStatus(ExtractionStatus.AiProcessing);
        await db.SaveChangesAsync(ct);
        progressService.Publish(msg.SessionId, Progress(msg, "ai_started",
            "Bruker AI for å finne flere kontakter...", progress: 0.55));

        try
        {
            var truncated = rawText[..Math.Min(rawText.Length, llmSettings.MaxInputCharacters)];
            var llmResult = await llmService.ExtractContactsAsync(truncated, $"Fil: {msg.FileName}", ct);
            var merged = ContactMergeHelper.Merge(msg.SessionId, regexContacts, llmResult);

            progressService.Publish(msg.SessionId, Progress(msg, "ai_complete",
                $"AI fant {merged.Count - regexContacts.Count} ekstra kontakter.", merged.Count, 0.85));

            return (merged, true);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AI-ekstraksjon feilet for sesjon {SessionId} – bruker regex-resultater",
                msg.SessionId);
            return (regexContacts, false);
        }
    }

    private async Task SaveFailureStatusAsync(Guid sessionId, string errorMessage, CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var session = await db.UploadSessions.FindAsync([sessionId], ct);
            if (session is not null)
            {
                session.UpdateStatus(ExtractionStatus.Failed, errorMessage);
                await db.SaveChangesAsync(ct);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Kunne ikke lagre feilstatus for sesjon {SessionId}", sessionId);
        }
    }

    private void DeleteTempFile(string filePath)
    {
        try { if (File.Exists(filePath)) File.Delete(filePath); }
        catch (Exception ex) { logger.LogWarning(ex, "Kunne ikke slette midlertidig fil {Path}", filePath); }
    }

    private void ScheduleSseCleanup(Guid sessionId)
    {
        _ = Task.Delay(TimeSpan.FromSeconds(60), CancellationToken.None)
            .ContinueWith(_ => progressService.Remove(sessionId),
                CancellationToken.None, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Default);
    }

    private static SseProgressEvent Progress(ExtractionRequested msg, string stage, string message,
        int? contactsFound = null, double? progress = null) =>
        new(msg.SessionId, stage, message, contactsFound, progress);
}
