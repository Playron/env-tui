namespace ContactExtractor.Api.Services.Integrations;

public interface ICrmExporter
{
    string Name { get; }
    Task<CrmExportResult> ExportAsync(
        List<Contact> contacts,
        string apiKey,
        CancellationToken ct = default);
}

public record CrmExportResult(
    bool Success,
    int ContactsExported,
    string? ErrorMessage = null);
