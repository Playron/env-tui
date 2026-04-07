using ContactExtractor.Api.AI;
using Microsoft.Extensions.Options;
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
        if (ShouldUseAi(regexContacts))
        {
            try
            {
                var truncated = rawText[..Math.Min(rawText.Length, settings.Value.MaxInputCharacters)];
                var llmResult = await llmService.ExtractContactsAsync(truncated, $"PDF-fil: {fileName}", ct);
                return MergeResults(sessionId, regexContacts, llmResult);
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
        return string.Join("\n", doc.GetPages().Select(p => p.Text));
    }

    private static MemoryStream BufferStream(Stream source)
    {
        var ms = new MemoryStream();
        source.CopyTo(ms);
        ms.Position = 0;
        return ms;
    }

    private static bool ShouldUseAi(List<Contact> regexContacts) =>
        regexContacts.Count < 2 || regexContacts.Any(c => c.Confidence < 0.5);

    private static List<Contact> MergeResults(
        Guid sessionId, List<Contact> regexContacts, LlmExtractionResult llmResult)
    {
        var merged = new List<Contact>(regexContacts);

        foreach (var llm in llmResult.Contacts)
        {
            var isDuplicate = regexContacts.Any(r =>
                (!string.IsNullOrEmpty(r.Email) &&
                 string.Equals(r.Email, llm.Email, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(r.FullName) &&
                 string.Equals(r.FullName, llm.FullName, StringComparison.OrdinalIgnoreCase)));

            if (isDuplicate) continue;

            var contact = new Contact(sessionId)
            {
                FullName = llm.FullName,
                FirstName = llm.FirstName,
                LastName = llm.LastName,
                Organization = llm.Organization,
                Title = llm.Title,
                Address = llm.Address,
                Confidence = llmResult.OverallConfidence * 0.9,
                ExtractionSource = "ai"
            };
            contact.SetEmail(EmailAddress.TryCreate(llm.Email));
            contact.SetPhone(PhoneNumber.TryCreate(llm.Phone));
            merged.Add(contact);
        }

        return merged;
    }
}
