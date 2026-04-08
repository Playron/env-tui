namespace ContactExtractor.Api.Services.Parsers;

public class TextParser(
    ContactExtractionService extractionService,
    ILlmService llmService,
    IOptions<LlmSettings> settings) : IFileParser
{
    public bool CanParse(string fileExtension) => fileExtension is ".txt" or ".text";

    public async Task<List<Contact>> ParseAsync(Stream fileStream, string fileName, CancellationToken ct = default)
    {
        var sessionId = Guid.CreateVersion7();
        using var reader = new StreamReader(fileStream);
        var rawText = await reader.ReadToEndAsync(ct);

        if (string.IsNullOrWhiteSpace(rawText)) return [];

        var regexContacts = extractionService.ExtractFromText(rawText, sessionId);

        if (ContactMergeHelper.ShouldUseAi(regexContacts))
        {
            try
            {
                var truncated = rawText[..Math.Min(rawText.Length, settings.Value.MaxInputCharacters)];
                var llmResult = await llmService.ExtractContactsAsync(truncated, $"Tekstfil: {fileName}", ct);
                return ContactMergeHelper.Merge(sessionId, regexContacts, llmResult);
            }
            catch
            {
                // Fallback to regex on AI failure
            }
        }

        return regexContacts;
    }

    public async Task<PreviewResultDto> PreviewAsync(Stream fileStream, string fileName, CancellationToken ct = default)
    {
        using var reader = new StreamReader(fileStream);
        var lines = new List<string>();
        while (lines.Count < 10 && !reader.EndOfStream)
        {
            ct.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(ct);
            if (line is not null) lines.Add(line);
        }

        var preview = lines.Take(5)
            .Select(l => new Dictionary<string, string> { ["text"] = l })
            .ToList();

        return new PreviewResultDto(fileName, ".txt", ["text"], preview, []);
    }

    public async Task<(List<Contact> Contacts, string RawText)> ParseWithoutAiAsync(
        Stream fileStream, string fileName, CancellationToken ct = default)
    {
        var sessionId = Guid.CreateVersion7();
        using var reader = new StreamReader(fileStream);
        var rawText = await reader.ReadToEndAsync(ct);
        var contacts = string.IsNullOrWhiteSpace(rawText)
            ? []
            : extractionService.ExtractFromText(rawText, sessionId);
        return (contacts, rawText);
    }
}
