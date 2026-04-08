namespace ContactExtractor.Api.Contracts;

public record DashboardDto(
    int TotalSessions,
    int TotalContacts,
    int SessionsThisMonth,
    int ContactsThisMonth,
    int AiExtractions,
    int DuplicatesFound,
    int DuplicatesResolved,
    List<FileTypeBreakdown> ByFileType,
    List<DailyActivity> ActivityLast30Days);

public record FileTypeBreakdown(string FileType, int Count);

public record DailyActivity(string Date, int Uploads, int Contacts);
