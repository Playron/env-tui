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
            progressService.Publish(msg.SessionId, new SseProgressEvent(
                msg.SessionId, "extracting", "Leser fil og kjører regex-ekstraksjon...", null, 0.1));

            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var extractionService = scope.ServiceProvider.GetRequiredService<ContactExtractionService>();
            var parserFactory = scope.ServiceProvider.GetRequiredService<FileParserFactory>();
            var llmService = scope.ServiceProvider.GetRequiredService<ILlmService>();
            var llmSettings = scope.ServiceProvider.GetRequiredService<IOptions<LlmSettings>>().Value;

            var session = await db.UploadSessions.FindAsync([msg.SessionId], ct);
            if (session is null)
            {
                logger.LogWarning("Sesjon {SessionId} finnes ikke i DB – hopper over", msg.SessionId);
                return;
            }

            session.UpdateStatus(ExtractionStatus.Extracting);
            await db.SaveChangesAsync(ct);

            // Steg 1: Les råtekst og kjør regex
            string rawText;
            List<Contact> regexContacts;

            var parser = parserFactory.GetParser(msg.FileExtension);
            if (parser is not null)
            {
                await using var fs = File.OpenRead(msg.FilePath);
                (regexContacts, rawText) = await parser.ParseWithoutAiAsync(fs, msg.FileName, ct);
            }
            else
            {
                // Ukjent format – prøv å lese som tekst
                rawText = await File.ReadAllTextAsync(msg.FilePath, ct);
                regexContacts = extractionService.ExtractFromText(rawText, msg.SessionId);
            }

            progressService.Publish(msg.SessionId, new SseProgressEvent(
                msg.SessionId, "regex_done",
                $"Regex fant {regexContacts.Count} kontakter.", regexContacts.Count, 0.4));

            // Steg 2: AI hvis nødvendig
            var finalContacts = regexContacts;
            var usedAi = false;

            if (!string.IsNullOrWhiteSpace(rawText) && ContactMergeHelper.ShouldUseAi(regexContacts))
            {
                session.UpdateStatus(ExtractionStatus.AiProcessing);
                await db.SaveChangesAsync(ct);

                progressService.Publish(msg.SessionId, new SseProgressEvent(
                    msg.SessionId, "ai_started", "Bruker AI for å finne flere kontakter...", null, 0.55));

                try
                {
                    var truncated = rawText[..Math.Min(rawText.Length, llmSettings.MaxInputCharacters)];
                    var llmResult = await llmService.ExtractContactsAsync(
                        truncated, $"Fil: {msg.FileName}", ct);

                    finalContacts = ContactMergeHelper.Merge(msg.SessionId, regexContacts, llmResult);
                    usedAi = true;

                    progressService.Publish(msg.SessionId, new SseProgressEvent(
                        msg.SessionId, "ai_complete",
                        $"AI fant {finalContacts.Count - regexContacts.Count} ekstra kontakter.",
                        finalContacts.Count, 0.85));
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "AI-ekstraksjon feilet for sesjon {SessionId} – bruker regex-resultater",
                        msg.SessionId);
                    // Fortsett med regex-resultater
                }
            }

            // Steg 3: Lagre i DB
            session.AddContacts(finalContacts);
            session.SetRowsProcessed(finalContacts.Count);
            session.SetUsedAi(usedAi);
            session.UpdateStatus(ExtractionStatus.Completed);
            await db.SaveChangesAsync(ct);

            logger.LogInformation(
                "Sesjon {SessionId} fullført: {Count} kontakter, AI brukt: {UsedAi}",
                msg.SessionId, finalContacts.Count, usedAi);

            progressService.Complete(msg.SessionId, new SseProgressEvent(
                msg.SessionId, "done",
                $"Ferdig! {finalContacts.Count} kontakter ekstrahert.",
                finalContacts.Count, 1.0));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ekstraksjon feilet for sesjon {SessionId}", msg.SessionId);

            progressService.Complete(msg.SessionId, new SseProgressEvent(
                msg.SessionId, "failed", $"Feil: {ex.Message}", null, null));

            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var session = await db.UploadSessions.FindAsync([msg.SessionId], ct);
                if (session is not null)
                {
                    session.UpdateStatus(ExtractionStatus.Failed, ex.Message);
                    await db.SaveChangesAsync(ct);
                }
            }
            catch (Exception dbEx)
            {
                logger.LogError(dbEx, "Kunne ikke lagre feilstatus for sesjon {SessionId}", msg.SessionId);
            }
        }
        finally
        {
            // Rydd opp midlertidig fil
            if (File.Exists(msg.FilePath))
            {
                try { File.Delete(msg.FilePath); }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Kunne ikke slette midlertidig fil {Path}", msg.FilePath);
                }
            }

            // Fjern SSE-channel etter forsinkelse – gir forsinkede klienter tid til å koble til
            _ = Task.Delay(TimeSpan.FromSeconds(60), CancellationToken.None)
                .ContinueWith(_ => progressService.Remove(msg.SessionId),
                    CancellationToken.None,
                    TaskContinuationOptions.OnlyOnRanToCompletion,
                    TaskScheduler.Default);
        }
    }
}
