namespace ContactExtractor.Api.AI;

public interface ILlmService
{
    Task<LlmExtractionResult> ExtractContactsAsync(
        string rawText,
        string? fileContext = null,
        CancellationToken ct = default);
}
