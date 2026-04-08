using ContactExtractor.Api.Messaging.Messages;
using ContactExtractor.Api.Services;
using MassTransit;

namespace ContactExtractor.Api.Messaging.Consumers;

public class DuplicateScanConsumer(
    IServiceScopeFactory scopeFactory,
    ILogger<DuplicateScanConsumer> logger) : IConsumer<DuplicateScanRequested>
{
    public async Task Consume(ConsumeContext<DuplicateScanRequested> context)
    {
        var msg = context.Message;
        var ct  = context.CancellationToken;

        logger.LogInformation("Starter duplikatsøk for sesjon {SessionId}", msg.SessionId);

        using var scope = scopeFactory.CreateScope();
        var db        = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var detector  = scope.ServiceProvider.GetRequiredService<DuplicateDetectionService>();

        // Hent alle kontakter for brukeren via sesjon-IDs
        var sessionIds = await db.UploadSessions
            .Where(s => s.UserId == msg.UserId)
            .Select(s => s.Id)
            .ToListAsync(ct);

        var contacts = await db.Contacts
            .Where(c => sessionIds.Contains(c.UploadSessionId))
            .ToListAsync(ct);

        if (contacts.Count < 2)
        {
            logger.LogInformation("Færre enn 2 kontakter for bruker {UserId} – hopper over duplikatsøk", msg.UserId);
            return;
        }

        var groups = detector.FindDuplicates(contacts);

        foreach (var (indices, score) in groups)
        {
            var groupContacts = indices.Select(i => contacts[i]).ToList();
            var group = new DuplicateGroup(msg.UserId, score, groupContacts);

            // Oppdater DuplicateGroupId på kontaktene
            foreach (var contact in groupContacts)
                contact.DuplicateGroupId = group.Id;

            db.DuplicateGroups.Add(group);
        }

        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Duplikatsøk fullført for sesjon {SessionId}: {Count} grupper funnet",
            msg.SessionId, groups.Count);
    }
}
