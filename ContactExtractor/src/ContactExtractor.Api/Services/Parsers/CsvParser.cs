using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;

namespace ContactExtractor.Api.Services.Parsers;

public class CsvParser(ContactExtractionService extractionService) : IFileParser
{
    public bool CanParse(string fileExtension) =>
        fileExtension is ".csv" or ".tsv";

    public async Task<List<Contact>> ParseAsync(Stream fileStream, string fileName, CancellationToken ct = default)
    {
        var sessionId = Guid.CreateVersion7();
        using var reader = new StreamReader(fileStream);

        var delimiter = fileName.EndsWith(".tsv") ? "\t" : ",";
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = delimiter,
            HasHeaderRecord = true,
            MissingFieldFound = null,
            BadDataFound = null
        };

        using var csv = new CsvReader(reader, config);
        await csv.ReadAsync();
        csv.ReadHeader();

        var headers = csv.HeaderRecord ?? [];

        // Collect all rows first, then build mappings using sample data
        var allRows = new List<string[]>();
        while (await csv.ReadAsync())
        {
            ct.ThrowIfCancellationRequested();
            allRows.Add(headers.Select(h => csv.GetField(h)?.Trim() ?? "").ToArray());
        }

        // Build column→field mappings using header + first-5-row samples
        var mappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < headers.Length; i++)
        {
            var sampleValues = allRows.Take(5).Select(r => i < r.Length ? r[i] : "");
            var detected = extractionService.DetectColumnMapping(headers[i], sampleValues);
            if (detected is not null)
                mappings[headers[i]] = detected;
        }

        var contacts = new List<Contact>();
        foreach (var row in allRows)
        {
            var contact = new Contact(sessionId) { Confidence = 0.9, ExtractionSource = "regex" };
            for (var i = 0; i < headers.Length; i++)
            {
                if (!mappings.TryGetValue(headers[i], out var field)) continue;
                var value = i < row.Length ? row[i] : "";
                if (string.IsNullOrEmpty(value)) continue;
                ApplyField(contact, field, value);
            }
            if (HasAnyData(contact))
                contacts.Add(contact);
        }

        return contacts;
    }

    public async Task<PreviewResultDto> PreviewAsync(Stream fileStream, string fileName, CancellationToken ct = default)
    {
        using var reader = new StreamReader(fileStream);
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null,
            BadDataFound = null
        };

        using var csv = new CsvReader(reader, config);
        await csv.ReadAsync();
        csv.ReadHeader();

        var headers = csv.HeaderRecord?.ToList() ?? [];
        var sampleRows = new List<Dictionary<string, string>>();
        var sampleData = new Dictionary<string, List<string>>();

        foreach (var h in headers) sampleData[h] = [];

        var rowCount = 0;
        while (await csv.ReadAsync() && rowCount < 5)
        {
            ct.ThrowIfCancellationRequested();
            var row = new Dictionary<string, string>();
            foreach (var h in headers)
            {
                var val = csv.GetField(h) ?? "";
                row[h] = val;
                sampleData[h].Add(val);
            }
            sampleRows.Add(row);
            rowCount++;
        }

        var mappings = headers.Select(h => new ColumnMappingDto(
            h,
            extractionService.DetectColumnMapping(h, sampleData.GetValueOrDefault(h) ?? []),
            sampleData.GetValueOrDefault(h)?.ToArray() ?? []
        )).ToList();

        return new PreviewResultDto(fileName, ".csv", headers, sampleRows, mappings);
    }

    internal static void ApplyField(Contact contact, string field, string value)
    {
        switch (field)
        {
            case "FirstName":    contact.FirstName = value; break;
            case "LastName":     contact.LastName = value; break;
            case "FullName":     contact.FullName = value; break;
            case "Email":        contact.SetEmail(EmailAddress.TryCreate(value)); break;
            case "Phone":        contact.SetPhone(PhoneNumber.TryCreate(value)); break;
            case "Organization": contact.Organization = value; break;
            case "Title":        contact.Title = value; break;
            case "Address":      contact.Address = value; break;
        }
    }

    private static bool HasAnyData(Contact c) =>
        c.FullName is not null || c.FirstName is not null ||
        c.Email is not null || c.Phone is not null;
}
