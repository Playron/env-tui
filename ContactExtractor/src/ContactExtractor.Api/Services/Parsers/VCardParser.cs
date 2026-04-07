using System.Text.RegularExpressions;

namespace ContactExtractor.Api.Services.Parsers;

public class VCardParser : IFileParser
{
    public bool CanParse(string fileExtension) => fileExtension is ".vcf" or ".vcard";

    public async Task<List<Contact>> ParseAsync(Stream fileStream, string fileName, CancellationToken ct = default)
    {
        var sessionId = Guid.CreateVersion7();
        using var reader = new StreamReader(fileStream);
        var content = await reader.ReadToEndAsync(ct);

        return ParseVCards(content, sessionId);
    }

    public async Task<PreviewResultDto> PreviewAsync(Stream fileStream, string fileName, CancellationToken ct = default)
    {
        using var reader = new StreamReader(fileStream);
        var content = await reader.ReadToEndAsync(ct);

        var cards = ParseVCards(content, Guid.Empty);
        var sampleRows = cards.Take(5).Select(c => new Dictionary<string, string>
        {
            ["FN"]  = c.FullName ?? "",
            ["EMAIL"] = c.Email ?? "",
            ["TEL"] = c.Phone ?? "",
            ["ORG"] = c.Organization ?? ""
        }).ToList();

        return new PreviewResultDto(
            fileName, ".vcf",
            ["FN", "EMAIL", "TEL", "ORG"],
            sampleRows,
            [
                new ColumnMappingDto("FN",    "FullName",     sampleRows.Select(r => r["FN"]).ToArray()),
                new ColumnMappingDto("EMAIL", "Email",        sampleRows.Select(r => r["EMAIL"]).ToArray()),
                new ColumnMappingDto("TEL",   "Phone",        sampleRows.Select(r => r["TEL"]).ToArray()),
                new ColumnMappingDto("ORG",   "Organization", sampleRows.Select(r => r["ORG"]).ToArray())
            ]);
    }

    private static List<Contact> ParseVCards(string content, Guid sessionId)
    {
        var contacts = new List<Contact>();
        var cards = Regex.Split(content, @"BEGIN:VCARD", RegexOptions.IgnoreCase)
                         .Where(c => c.Contains("END:VCARD", StringComparison.OrdinalIgnoreCase));

        foreach (var card in cards)
        {
            var contact = new Contact(sessionId) { Confidence = 0.95 };
            var lines = card.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var colonIdx = line.IndexOf(':');
                if (colonIdx < 0) continue;

                var key = line[..colonIdx].Split(';')[0].ToUpperInvariant().Trim();
                var value = line[(colonIdx + 1)..].Trim();

                switch (key)
                {
                    case "FN":
                        contact.FullName = UnescapeVCard(value);
                        break;
                    case "N":
                        // N:LastName;FirstName;Additional;Prefix;Suffix
                        var parts = value.Split(';');
                        if (parts.Length >= 2)
                        {
                            contact.LastName  = UnescapeVCard(parts[0]);
                            contact.FirstName = UnescapeVCard(parts[1]);
                        }
                        break;
                    case "EMAIL":
                        if (contact.Email is null)
                            contact.SetEmail(EmailAddress.TryCreate(value));
                        break;
                    case "TEL":
                        if (contact.Phone is null)
                            contact.SetPhone(PhoneNumber.TryCreate(value));
                        break;
                    case "ORG":
                        contact.Organization = UnescapeVCard(value.Split(';')[0]);
                        break;
                    case "TITLE":
                        contact.Title = UnescapeVCard(value);
                        break;
                    case "ADR":
                        // ADR:;;Street;City;Region;PostalCode;Country
                        var adrParts = value.Split(';').Select(UnescapeVCard).ToArray();
                        contact.Address = string.Join(", ",
                            adrParts.Where(p => !string.IsNullOrWhiteSpace(p)));
                        break;
                }
            }

            if (contact.FullName is not null || contact.Email is not null || contact.Phone is not null)
                contacts.Add(contact);
        }

        return contacts;
    }

    private static string UnescapeVCard(string value) =>
        value.Replace(@"\n", "\n")
             .Replace(@"\N", "\n")
             .Replace(@"\,", ",")
             .Replace(@"\;", ";")
             .Replace(@"\\", "\\")
             .Trim();
}
