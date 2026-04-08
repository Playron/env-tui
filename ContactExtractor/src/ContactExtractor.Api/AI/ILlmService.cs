namespace ContactExtractor.Api.AI;

public interface ILlmService
{
    Task<LlmExtractionResult> ExtractContactsAsync(
        string rawText,
        string? fileContext = null,
        CancellationToken ct = default);

    Task<Dictionary<string, NormalizedName>> NormalizeNamesAsync(
        List<string> rawNames,
        CancellationToken ct = default);
}

public record NormalizedName(
    string? FirstName,
    string? LastName,
    string? Title = null);
