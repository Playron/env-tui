using System.Text.RegularExpressions;

namespace ContactExtractor.Api.Services;

public class ContactExtractionService
{
    // Norske og engelske kolonnenavn-aliaser
    public static readonly Dictionary<string, string[]> ColumnAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["FirstName"]    = ["fornavn", "firstname", "first name", "first_name", "givenname", "given_name"],
        ["LastName"]     = ["etternavn", "lastname", "last name", "last_name", "surname", "familyname", "family_name"],
        ["FullName"]     = ["navn", "name", "fullt navn", "fullname", "full name", "kontakt", "contact"],
        ["Email"]        = ["e-post", "epost", "email", "e_post", "mail", "e-mail", "epostadresse"],
        ["Phone"]        = ["telefon", "tlf", "phone", "mobil", "mobile", "mobilnr", "telefonnummer", "tel", "celular", "cell"],
        ["Organization"] = ["organisasjon", "org", "firma", "company", "organization", "organisation", "bedrift", "arbeidsgiver"],
        ["Title"]        = ["tittel", "stilling", "title", "role", "rolle", "jobtitle", "job title", "job_title"],
        ["Address"]      = ["adresse", "address", "postadresse", "gateadresse", "street"]
    };

    // Regex-mønstre med raw string literals
    public static readonly Regex EmailRegex = new(
        """[a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,}""",
        RegexOptions.Compiled);

    public static readonly Regex NorwegianPhoneRegex = new(
        """(?:\+47[\s\-]?)?(?:\d{2}[\s\-]?\d{2}[\s\-]?\d{2}[\s\-]?\d{2}|\d{3}[\s\-]?\d{2}[\s\-]?\d{3})""",
        RegexOptions.Compiled);

    public static readonly Regex GenericPhoneRegex = new(
        """\+?[\d\s\-\(\)]{7,20}""",
        RegexOptions.Compiled);

    /// <summary>
    /// Determine which field a column header maps to.
    /// Priority 1: exact match, Priority 2: fuzzy/alias match, Priority 3: content-based inference
    /// </summary>
    public string? DetectColumnMapping(string columnHeader, IEnumerable<string> sampleValues)
    {
        var normalized = columnHeader.ToLowerInvariant().Trim();

        // Priority 1 & 2: header-based matching
        foreach (var (field, aliases) in ColumnAliases)
        {
            if (aliases.Any(a => string.Equals(a, normalized, StringComparison.OrdinalIgnoreCase)))
                return field;
        }

        // Partial match
        foreach (var (field, aliases) in ColumnAliases)
        {
            if (aliases.Any(a => normalized.Contains(a, StringComparison.OrdinalIgnoreCase) ||
                                 a.Contains(normalized, StringComparison.OrdinalIgnoreCase)))
                return field;
        }

        // Priority 3: content-based inference from sample values
        var samples = sampleValues.Where(v => !string.IsNullOrWhiteSpace(v)).Take(5).ToList();
        if (samples.Count == 0) return null;

        var emailMatches = samples.Count(v => EmailAddress.IsValid(v));
        if (emailMatches >= samples.Count * 0.6) return "Email";

        var phoneMatches = samples.Count(v => PhoneNumber.IsValid(v));
        if (phoneMatches >= samples.Count * 0.6) return "Phone";

        return null;
    }

    /// <summary>
    /// Extract contacts from unstructured text using regex heuristics.
    /// </summary>
    public List<Contact> ExtractFromText(string text, Guid sessionId)
    {
        var contacts = new List<Contact>();
        var lines = text.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries);

        var emailMatches = EmailRegex.Matches(text);
        var phoneMatches = NorwegianPhoneRegex.Matches(text);

        // Group emails/phones with nearby names
        foreach (Match emailMatch in emailMatches)
        {
            var contact = new Contact(sessionId) { Confidence = 0.6 };
            contact.SetEmail(EmailAddress.TryCreate(emailMatch.Value));

            // Find nearest name-like text (within 3 lines of email occurrence)
            var emailLine = GetLineNumber(text, emailMatch.Index);
            var nearbyName = FindNearbyName(lines, emailLine);
            if (nearbyName is not null)
            {
                contact.FullName = nearbyName;
                contact.Confidence = 0.75;
            }

            // Find nearby phone
            var nearbyPhone = phoneMatches
                .Cast<Match>()
                .MinBy(p => Math.Abs(p.Index - emailMatch.Index));

            if (nearbyPhone is not null && Math.Abs(nearbyPhone.Index - emailMatch.Index) < 200)
            {
                contact.SetPhone(PhoneNumber.TryCreate(nearbyPhone.Value));
            }

            contacts.Add(contact);
        }

        // Add phone-only contacts not already captured
        foreach (Match phoneMatch in phoneMatches)
        {
            var alreadyCaptured = contacts.Any(c =>
                c.Phone == new string(phoneMatch.Value.Where(ch => char.IsDigit(ch) || ch is '+').ToArray()));

            if (alreadyCaptured) continue;

            var contact = new Contact(sessionId) { Confidence = 0.5 };
            contact.SetPhone(PhoneNumber.TryCreate(phoneMatch.Value));

            var phoneLine = GetLineNumber(text, phoneMatch.Index);
            var nearbyName = FindNearbyName(lines, phoneLine);
            if (nearbyName is not null)
            {
                contact.FullName = nearbyName;
                contact.Confidence = 0.65;
            }

            contacts.Add(contact);
        }

        return contacts;
    }

    private static int GetLineNumber(string text, int index)
    {
        var sub = text[..index];
        return sub.Count(c => c == '\n');
    }

    private static string? FindNearbyName(string[] lines, int targetLine)
    {
        var start = Math.Max(0, targetLine - 2);
        var end = Math.Min(lines.Length - 1, targetLine + 2);

        for (var i = start; i <= end; i++)
        {
            var line = lines[i].Trim();
            // A name-like line: 2-5 words, no @, mostly letters
            if (line.Length > 3 && !line.Contains('@') &&
                !EmailRegex.IsMatch(line) &&
                !NorwegianPhoneRegex.IsMatch(line))
            {
                var words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (words.Length is >= 1 and <= 5 &&
                    words.All(w => w.All(c => char.IsLetter(c) || c is '-' or '\'')))
                    return line;
            }
        }
        return null;
    }
}
