namespace ContactExtractor.Api.Services;

/// <summary>
/// Normaliserer navn via LLM (AI) som post-prosessering etter ekstraksjon.
/// </summary>
public class NameNormalizationService(ILlmService llmService, ILogger<NameNormalizationService> logger)
{
    /// <summary>
    /// Normaliserer navn på alle kontakter med lavt confidence.
    /// Returnerer antall normaliserte kontakter.
    /// </summary>
    public async Task<int> NormalizeAsync(
        IList<Contact> contacts,
        CancellationToken ct = default)
    {
        // Finn kontakter med uspesifisert navn-format (store bokstaver, invertert rekkefølge etc.)
        var toNormalize = contacts
            .Where(c => NeedsNormalization(c))
            .Select(c => GetRawName(c))
            .Where(n => n is not null)
            .Distinct()
            .ToList()!;

        if (toNormalize.Count == 0) return 0;

        try
        {
            var normalized = await llmService.NormalizeNamesAsync(toNormalize!, ct);
            var count = 0;

            foreach (var contact in contacts)
            {
                var rawName = GetRawName(contact);
                if (rawName is null) continue;

                if (normalized.TryGetValue(rawName, out var result))
                {
                    contact.FirstName = result.FirstName ?? contact.FirstName;
                    contact.LastName  = result.LastName  ?? contact.LastName;
                    if (result.Title is not null && contact.Title is null)
                        contact.Title = result.Title;
                    contact.FullName = $"{contact.FirstName} {contact.LastName}".Trim();
                    count++;
                }
            }

            return count;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Navnenormalisering feilet");
            return 0;
        }
    }

    private static bool NeedsNormalization(Contact c)
    {
        var name = c.FullName ?? $"{c.FirstName} {c.LastName}";
        if (string.IsNullOrWhiteSpace(name)) return false;

        // Sjekk for invertert format "ETTERNAVN, Fornavn"
        if (name.Contains(',')) return true;

        // Sjekk for all-caps
        if (name == name.ToUpperInvariant() && name.Length > 2) return true;

        // Sjekk for titler i navn
        if (name.StartsWith("Dr.", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("Prof.", StringComparison.OrdinalIgnoreCase)) return true;

        return false;
    }

    private static string? GetRawName(Contact c)
    {
        if (!string.IsNullOrWhiteSpace(c.FullName)) return c.FullName;
        var parts = new[] { c.FirstName, c.LastName }.Where(p => !string.IsNullOrWhiteSpace(p));
        var name = string.Join(" ", parts);
        return string.IsNullOrWhiteSpace(name) ? null : name;
    }
}
