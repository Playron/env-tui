using ContactExtractor.Api.AI;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Extensions.Options;

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

        if (ShouldUseAi(regexContacts))
        {
            try
            {
                var truncated = rawText[..Math.Min(rawText.Length, settings.Value.MaxInputCharacters)];
                var llmResult = await llmService.ExtractContactsAsync(truncated, $"Word-dokument: {fileName}", ct);
                return MergeResults(sessionId, regexContacts, llmResult);
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
