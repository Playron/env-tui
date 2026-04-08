using UglyToad.PdfPig;

namespace ContactExtractor.Api.Services.Parsers;

public class PdfParser(
    ContactExtractionService extractionService,
    ILlmService llmService,
    IOptions<LlmSettings> settings) : IFileParser
{
    public bool CanParse(string fileExtension) => fileExtension is ".pdf";

    public async Task<List<Contact>> ParseAsync(Stream fileStream, string fileName, CancellationToken ct = default)
    {
        var sessionId = Guid.CreateVersion7();
        var rawText = ExtractText(fileStream);

        if (string.IsNullOrWhiteSpace(rawText)) return [];

        // Steg 1: Regex-basert ekstraksjon
        var regexContacts = extractionService.ExtractFromText(rawText, sessionId);

        // Steg 2: AI-fallback hvis regex gir lite eller usikkert resultat
        if (ContactMergeHelper.ShouldUseAi(regexContacts))
        {
            try
            {
                var truncated = rawText[..Math.Min(rawText.Length, settings.Value.MaxInputCharacters)];
                var llmResult = await llmService.ExtractContactsAsync(truncated, $"PDF-fil: {fileName}", ct);
                return ContactMergeHelper.Merge(sessionId, regexContacts, llmResult);
            }
            catch
            {
                // Fallback: return regex results on AI failure
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

        return Task.FromResult(new PreviewResultDto(fileName, ".pdf", ["text"], lines, []));
    }

    private static string ExtractText(Stream stream)
    {
        Stream pdf = stream.CanSeek ? stream : BufferStream(stream);
        using var doc = PdfDocument.Open(pdf);
        var sb = new System.Text.StringBuilder();
        foreach (var page in doc.GetPages())
        {
            // Group words by Y position to reconstruct lines
            var words = page.GetWords().ToList();
            if (words.Count == 0) continue;

            var lines = new List<List<UglyToad.PdfPig.Content.Word>>();
            var currentLine = new List<UglyToad.PdfPig.Content.Word> { words[0] };
            for (var i = 1; i < words.Count; i++)
            {
                // Words on the same line have similar Y coordinates (within tolerance)
                if (Math.Abs(words[i].BoundingBox.Bottom - currentLine[0].BoundingBox.Bottom) < 2)
                {
                    currentLine.Add(words[i]);
                }
                else
                {
                    lines.Add(currentLine);
                    currentLine = [words[i]];
                }
            }
            lines.Add(currentLine);

            foreach (var line in lines)
            {
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(string.Join(" ", line.Select(w => w.Text)));
            }
        }
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
