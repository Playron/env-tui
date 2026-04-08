using ContactExtractor.Api.Auth;

namespace ContactExtractor.Api.Endpoints;

public static class DashboardEndpoints
{
    public static void MapDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/dashboard")
            .WithTags("Dashboard");

        group.MapGet("/", GetDashboard)
            .Produces<DashboardDto>(200)
            .WithSummary("Hent dashboard-statistikk for brukeren");

        group.MapGet("/audit", GetAuditLog)
            .Produces<List<AuditLogDto>>(200)
            .WithSummary("Hent audit-logg for brukeren");

        group.MapGet("/audit/admin", GetAdminAuditLog)
            .Produces<List<AuditLogDto>>(200)
            .WithSummary("Hent all audit-logg (admin-only)");
    }

    private static async Task<Ok<DashboardDto>> GetDashboard(
        AppDbContext db,
        CurrentUserService currentUser,
        CancellationToken ct)
    {
        var userId = currentUser.UserIdOrAnonymous;
        var now    = DateTime.UtcNow;
        var month  = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var thirty = now.AddDays(-30);

        var sessions = await db.UploadSessions
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .Select(s => new
            {
                s.Id,
                s.FileType,
                s.TotalRowsProcessed,
                s.UsedAi,
                s.CreatedAt
            })
            .ToListAsync(ct);

        var totalContacts      = await db.Contacts.AsNoTracking()
            .CountAsync(c => db.UploadSessions
                .Where(s => s.UserId == userId)
                .Select(s => s.Id)
                .Contains(c.UploadSessionId), ct);

        var sessionsThisMonth  = sessions.Count(s => s.CreatedAt >= month);
        var contactsThisMonth  = 0; // Simplified for performance
        var aiExtractions      = sessions.Count(s => s.UsedAi);

        var duplicatesFound    = await db.DuplicateGroups.AsNoTracking()
            .CountAsync(d => d.UserId == userId, ct);
        var duplicatesResolved = await db.DuplicateGroups.AsNoTracking()
            .CountAsync(d => d.UserId == userId && d.Resolved, ct);

        var byFileType = sessions
            .GroupBy(s => s.FileType)
            .Select(g => new FileTypeBreakdown(g.Key, g.Count()))
            .ToList();

        var last30 = Enumerable.Range(0, 30)
            .Select(i => thirty.AddDays(i).Date)
            .Select(date => new DailyActivity(
                date.ToString("yyyy-MM-dd"),
                sessions.Count(s => s.CreatedAt.Date == date),
                0)) // kontakter per dag krever join
            .ToList();

        return TypedResults.Ok(new DashboardDto(
            sessions.Count,
            totalContacts,
            sessionsThisMonth,
            contactsThisMonth,
            aiExtractions,
            duplicatesFound,
            duplicatesResolved,
            byFileType,
            last30));
    }

    private static async Task<Ok<List<AuditLogDto>>> GetAuditLog(
        AppDbContext db,
        CurrentUserService currentUser,
        CancellationToken ct)
    {
        var userId = currentUser.UserIdOrAnonymous;
        var entries = await db.AuditLog
            .AsNoTracking()
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.Timestamp)
            .Take(200)
            .Select(a => new AuditLogDto(a.Id, a.UserId, a.Action, a.EntityType, a.EntityId, a.Details, a.Timestamp))
            .ToListAsync(ct);

        return TypedResults.Ok(entries);
    }

    private static async Task<Ok<List<AuditLogDto>>> GetAdminAuditLog(
        AppDbContext db,
        CurrentUserService currentUser,
        string? userId = null,
        string? action = null,
        CancellationToken ct = default)
    {
        var query = db.AuditLog.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(userId))
            query = query.Where(a => a.UserId == userId);

        if (!string.IsNullOrWhiteSpace(action))
            query = query.Where(a => a.Action == action);

        var entries = await query
            .OrderByDescending(a => a.Timestamp)
            .Take(500)
            .Select(a => new AuditLogDto(a.Id, a.UserId, a.Action, a.EntityType, a.EntityId, a.Details, a.Timestamp))
            .ToListAsync(ct);

        return TypedResults.Ok(entries);
    }
}
