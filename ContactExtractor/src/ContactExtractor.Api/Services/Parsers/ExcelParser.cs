using OfficeOpenXml;

namespace ContactExtractor.Api.Services.Parsers;

public class ExcelParser(ContactExtractionService extractionService) : IFileParser
{
    static ExcelParser()
    {
        ExcelPackage.License.SetNonCommercialPersonal("ContactExtractor");
    }

    public bool CanParse(string fileExtension) =>
        fileExtension is ".xlsx" or ".xls";

    public async Task<List<Contact>> ParseAsync(Stream fileStream, string fileName, CancellationToken ct = default)
    {
        var sessionId = Guid.CreateVersion7();
        using var package = new ExcelPackage();
        await package.LoadAsync(fileStream, ct);

        var worksheet = package.Workbook.Worksheets.FirstOrDefault();
        if (worksheet is null) return [];

        var (headers, mappings) = GetHeaderMappings(worksheet);
        var contacts = new List<Contact>();

        for (var row = 2; row <= worksheet.Dimension?.Rows; row++)
        {
            ct.ThrowIfCancellationRequested();
            var contact = new Contact(sessionId) { Confidence = 0.9 };

            foreach (var (colIdx, field) in mappings)
            {
                var value = worksheet.Cells[row, colIdx].Text?.Trim();
                if (string.IsNullOrEmpty(value)) continue;

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

            if (HasAnyData(contact))
                contacts.Add(contact);
        }

        return contacts;
    }

    public Task<PreviewResultDto> PreviewAsync(Stream fileStream, string fileName, CancellationToken ct = default)
    {
        using var package = new ExcelPackage();
        package.Load(fileStream);

        var worksheet = package.Workbook.Worksheets.FirstOrDefault();
        if (worksheet is null)
            return Task.FromResult(new PreviewResultDto(fileName, ".xlsx", [], [], []));

        var headerRow = worksheet.Dimension?.Rows >= 1 ? 1 : 0;
        var headers = new List<string>();
        var sampleData = new Dictionary<string, List<string>>();

        for (var col = 1; col <= worksheet.Dimension?.Columns; col++)
        {
            var header = worksheet.Cells[headerRow, col].Text?.Trim() ?? $"Column{col}";
            headers.Add(header);
            sampleData[header] = [];
        }

        var sampleRows = new List<Dictionary<string, string>>();
        for (var row = 2; row <= Math.Min(6, worksheet.Dimension?.Rows ?? 1); row++)
        {
            var rowData = new Dictionary<string, string>();
            for (var col = 1; col <= headers.Count; col++)
            {
                var val = worksheet.Cells[row, col].Text?.Trim() ?? "";
                rowData[headers[col - 1]] = val;
                sampleData[headers[col - 1]].Add(val);
            }
            sampleRows.Add(rowData);
        }

        var mappings = headers.Select(h => new ColumnMappingDto(
            h,
            extractionService.DetectColumnMapping(h, sampleData.GetValueOrDefault(h) ?? []),
            sampleData.GetValueOrDefault(h)?.ToArray() ?? []
        )).ToList();

        return Task.FromResult(new PreviewResultDto(fileName, ".xlsx", headers, sampleRows, mappings));
    }

    private (List<string> headers, Dictionary<int, string> mappings) GetHeaderMappings(ExcelWorksheet worksheet)
    {
        var headers = new List<string>();
        var mappings = new Dictionary<int, string>();

        if (worksheet.Dimension is null) return (headers, mappings);

        var sampleData = new Dictionary<int, List<string>>();
        for (var col = 1; col <= worksheet.Dimension.Columns; col++)
        {
            var header = worksheet.Cells[1, col].Text?.Trim() ?? $"Column{col}";
            headers.Add(header);
            sampleData[col] = [];

            // Collect sample data for content-based detection
            for (var row = 2; row <= Math.Min(6, worksheet.Dimension.Rows); row++)
                sampleData[col].Add(worksheet.Cells[row, col].Text?.Trim() ?? "");

            var detected = extractionService.DetectColumnMapping(header, sampleData[col]);
            if (detected is not null)
                mappings[col] = detected;
        }

        return (headers, mappings);
    }

    private static bool HasAnyData(Contact c) =>
        c.FullName is not null || c.FirstName is not null ||
        c.Email is not null || c.Phone is not null;
}
