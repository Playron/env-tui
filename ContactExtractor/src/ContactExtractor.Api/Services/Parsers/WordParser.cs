using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace ContactExtractor.Api.Services.Parsers;

public class WordParser(
    ContactExtractionService extractionService,
    ILlmService llmService,
    IOptions<LlmSettings> settings) : IFileParser
{
    public bool CanParse(string fileExtension) => fileExtension is ".docx";

    public async Task<List<Contact>> ParseAsync(Stream fileStream, string fileName, CancellationToken ct = default)
    {
        var sessionId = Guid.CreateVersion7();
        var rawText = ExtractText(fileStream);

        if (string.IsNullOrWhiteSpace(rawText)) return [];

        var regexContacts = extractionService.ExtractFromText(rawText, sessionId);

        if (ContactMergeHelper.ShouldUseAi(regexContacts))
        {
            try
            {
                var truncated = rawText[..Math.Min(rawText.Length, settings.Value.MaxInputCharacters)];
                var llmResult = await llmService.ExtractContactsAsync(truncated, $"Word-dokument: {fileName}", ct);
                return ContactMergeHelper.Merge(sessionId, regexContacts, llmResult);
            }
            catch
            {
                // Fallback to regex on AI failure
            }
        }

        return regexContacts;
    }

    public Task<PreviewResultDto> PreviewAsync(Stream fileStream, string fileName, CancellationToken ct = default)
    {
        var text = ExtractText(fileStream);
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Take(5)
            .Select(l => new Dictionary<string, string> { ["text"] = l })
            .ToList();

        return Task.FromResult(new PreviewResultDto(fileName, ".docx", ["text"], lines, []));
    }

    private static string ExtractText(Stream stream)
    {
        Stream docStream = stream.CanSeek ? stream : BufferStream(stream);
        using var doc = WordprocessingDocument.Open(docStream, false);
        var sb = new System.Text.StringBuilder();
        foreach (var para in doc.MainDocumentPart?.Document?.Body?.Descendants<Paragraph>() ?? [])
            sb.AppendLine(para.InnerText);
        return sb.ToString();
    }

    private static MemoryStream BufferStream(Stream source)
    {
        var ms = new MemoryStream();
        source.CopyTo(ms);
        ms.Position = 0;
        return ms;
    }

    public Task<(List<Contact> Contacts, string RawText)> ParseWithoutAiAsync(
        Stream fileStream, string fileName, CancellationToken ct = default)
    {
        var sessionId = Guid.CreateVersion7();
        var rawText = ExtractText(fileStream);
        var contacts = string.IsNullOrWhiteSpace(rawText)
            ? []
            : extractionService.ExtractFromText(rawText, sessionId);
        return Task.FromResult((contacts, rawText));
    }
}
