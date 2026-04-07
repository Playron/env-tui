using ContactExtractor.Api.AI;
using Microsoft.Extensions.Options;

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

        if (ShouldUseAi(regexContacts))
        {
            try
            {
                var truncated = rawText[..Math.Min(rawText.Length, settings.Value.MaxInputCharacters)];
                var llmResult = await llmService.ExtractContactsAsync(truncated, $"Tekstfil: {fileName}", ct);
                return MergeResults(sessionId, regexContacts, llmResult);
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
