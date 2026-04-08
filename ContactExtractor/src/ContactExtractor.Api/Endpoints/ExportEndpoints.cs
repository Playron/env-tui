using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using OfficeOpenXml;

namespace ContactExtractor.Api.Endpoints;

public static class ExportEndpoints
{
    public static void MapExportEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/export")
            .WithTags("Export");

        group.MapPost("/{sessionId:guid}/csv", ExportCsv)
            .Produces<FileContentHttpResult>(200)
            .Produces(404)
            .WithSummary("Eksporter kontakter til CSV");

        group.MapPost("/{sessionId:guid}/excel", ExportExcel)
            .Produces<FileContentHttpResult>(200)
            .Produces(404)
            .WithSummary("Eksporter kontakter til Excel");

        group.MapPost("/{sessionId:guid}/vcard", ExportVCard)
            .Produces<FileContentHttpResult>(200)
            .Produces(404)
            .WithSummary("Eksporter kontakter til vCard (.vcf)");

        group.MapPost("/{sessionId:guid}/google", ExportGoogleCsv)
            .Produces<FileContentHttpResult>(200)
            .Produces(404)
            .WithSummary("Eksporter kontakter til Google Contacts CSV-format");

        group.MapPost("/{sessionId:guid}/outlook", ExportOutlookCsv)
            .Produces<FileContentHttpResult>(200)
            .Produces(404)
            .WithSummary("Eksporter kontakter til Outlook-kompatibel CSV");
    }

    private static async Task<Results<FileContentHttpResult, NotFound>> ExportCsv(
        Guid sessionId,
        AppDbContext db,
        CancellationToken ct)
    {
        var session = await db.UploadSessions
            .AsNoTracking()
            .Include(s => s.Contacts)
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct);

        if (session is null) return TypedResults.NotFound();

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true
        };

        using var ms = new MemoryStream();
        await using var writer = new StreamWriter(ms, Encoding.UTF8, leaveOpen: true);
        await using var csv = new CsvWriter(writer, config);

        csv.WriteField("Fornavn");
        csv.WriteField("Etternavn");
        csv.WriteField("Fullt navn");
        csv.WriteField("E-post");
        csv.WriteField("Telefon");
        csv.WriteField("Organisasjon");
        csv.WriteField("Stilling");
        csv.WriteField("Adresse");
        csv.WriteField("Konfidensverdi");
        await csv.NextRecordAsync();

        foreach (var contact in session.Contacts)
        {
            csv.WriteField(contact.FirstName);
            csv.WriteField(contact.LastName);
            csv.WriteField(contact.FullName);
            csv.WriteField(contact.Email);
            csv.WriteField(contact.Phone);
            csv.WriteField(contact.Organization);
            csv.WriteField(contact.Title);
            csv.WriteField(contact.Address);
            csv.WriteField(contact.Confidence.ToString("F2", CultureInfo.InvariantCulture));
            await csv.NextRecordAsync();
        }

        await writer.FlushAsync(ct);
        var bytes = ms.ToArray();
        var fileName = $"kontakter_{session.Id:N}.csv";

        return TypedResults.File(bytes, "text/csv", fileName);
    }

    private static async Task<Results<FileContentHttpResult, NotFound>> ExportExcel(
        Guid sessionId,
        AppDbContext db,
        CancellationToken ct)
    {
        ExcelPackage.License.SetNonCommercialPersonal("ContactExtractor");

        var session = await db.UploadSessions
            .AsNoTracking()
            .Include(s => s.Contacts)
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct);

        if (session is null) return TypedResults.NotFound();

        using var package = new ExcelPackage();
        var ws = package.Workbook.Worksheets.Add("Kontakter");

        // Header row
        string[] headers =
        [
            "Fornavn", "Etternavn", "Fullt navn", "E-post", "Telefon",
            "Organisasjon", "Stilling", "Adresse", "Konfidensverdi"
        ];

        for (var i = 0; i < headers.Length; i++)
        {
            ws.Cells[1, i + 1].Value = headers[i];
            ws.Cells[1, i + 1].Style.Font.Bold = true;
        }

        var row = 2;
        foreach (var c in session.Contacts)
        {
            ws.Cells[row, 1].Value = c.FirstName;
            ws.Cells[row, 2].Value = c.LastName;
            ws.Cells[row, 3].Value = c.FullName;
            ws.Cells[row, 4].Value = c.Email;
            ws.Cells[row, 5].Value = c.Phone;
            ws.Cells[row, 6].Value = c.Organization;
            ws.Cells[row, 7].Value = c.Title;
            ws.Cells[row, 8].Value = c.Address;
            ws.Cells[row, 9].Value = c.Confidence;
            row++;
        }

        ws.Cells[ws.Dimension?.Address ?? "A1"].AutoFitColumns();

        var bytes = await package.GetAsByteArrayAsync(ct);
        var fileName = $"kontakter_{session.Id:N}.xlsx";

        return TypedResults.File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }

    // ── POST /api/export/{sessionId}/vcard ────────────────────────────────────
    private static async Task<Results<FileContentHttpResult, NotFound>> ExportVCard(
        Guid sessionId,
        AppDbContext db,
        CancellationToken ct)
    {
        var session = await db.UploadSessions
            .AsNoTracking()
            .Include(s => s.Contacts)
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct);

        if (session is null) return TypedResults.NotFound();

        var sb = new StringBuilder();
        foreach (var c in session.Contacts)
        {
            sb.AppendLine("BEGIN:VCARD");
            sb.AppendLine("VERSION:3.0");
            var fn = c.FullName ?? $"{c.FirstName} {c.LastName}".Trim();
            if (!string.IsNullOrWhiteSpace(fn))
                sb.AppendLine($"FN:{fn}");
            if (!string.IsNullOrWhiteSpace(c.FirstName) || !string.IsNullOrWhiteSpace(c.LastName))
                sb.AppendLine($"N:{c.LastName ?? ""};{c.FirstName ?? ""};;;");
            if (!string.IsNullOrWhiteSpace(c.Email))
                sb.AppendLine($"EMAIL:{c.Email}");
            if (!string.IsNullOrWhiteSpace(c.Phone))
                sb.AppendLine($"TEL:{c.Phone}");
            if (!string.IsNullOrWhiteSpace(c.Organization))
                sb.AppendLine($"ORG:{c.Organization}");
            if (!string.IsNullOrWhiteSpace(c.Title))
                sb.AppendLine($"TITLE:{c.Title}");
            if (!string.IsNullOrWhiteSpace(c.Address))
                sb.AppendLine($"ADR:;;{c.Address};;;;");
            sb.AppendLine("END:VCARD");
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        return TypedResults.File(bytes, "text/vcard", $"kontakter_{session.Id:N}.vcf");
    }

    // ── POST /api/export/{sessionId}/google ───────────────────────────────────
    private static async Task<Results<FileContentHttpResult, NotFound>> ExportGoogleCsv(
        Guid sessionId,
        AppDbContext db,
        CancellationToken ct)
    {
        var session = await db.UploadSessions
            .AsNoTracking()
            .Include(s => s.Contacts)
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct);

        if (session is null) return TypedResults.NotFound();

        var config = new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = true };
        using var ms = new MemoryStream();
        await using var writer = new StreamWriter(ms, Encoding.UTF8, leaveOpen: true);
        await using var csv = new CsvWriter(writer, config);

        // Google Contacts CSV-format
        string[] headers = ["Name", "Given Name", "Family Name", "E-mail 1 - Value",
                            "Phone 1 - Value", "Organization 1 - Name", "Organization 1 - Title",
                            "Address 1 - Street"];
        foreach (var h in headers) csv.WriteField(h);
        await csv.NextRecordAsync();

        foreach (var c in session.Contacts)
        {
            csv.WriteField(c.FullName ?? $"{c.FirstName} {c.LastName}".Trim());
            csv.WriteField(c.FirstName);
            csv.WriteField(c.LastName);
            csv.WriteField(c.Email);
            csv.WriteField(c.Phone);
            csv.WriteField(c.Organization);
            csv.WriteField(c.Title);
            csv.WriteField(c.Address);
            await csv.NextRecordAsync();
        }

        await writer.FlushAsync(ct);
        return TypedResults.File(ms.ToArray(), "text/csv", $"google_kontakter_{session.Id:N}.csv");
    }

    // ── POST /api/export/{sessionId}/outlook ──────────────────────────────────
    private static async Task<Results<FileContentHttpResult, NotFound>> ExportOutlookCsv(
        Guid sessionId,
        AppDbContext db,
        CancellationToken ct)
    {
        var session = await db.UploadSessions
            .AsNoTracking()
            .Include(s => s.Contacts)
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct);

        if (session is null) return TypedResults.NotFound();

        var config = new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = true };
        using var ms = new MemoryStream();
        await using var writer = new StreamWriter(ms, Encoding.UTF8, leaveOpen: true);
        await using var csv = new CsvWriter(writer, config);

        // Outlook CSV-format
        string[] headers = ["First Name", "Last Name", "Display Name", "E-mail Address",
                            "Business Phone", "Company", "Job Title", "Business Street"];
        foreach (var h in headers) csv.WriteField(h);
        await csv.NextRecordAsync();

        foreach (var c in session.Contacts)
        {
            csv.WriteField(c.FirstName);
            csv.WriteField(c.LastName);
            csv.WriteField(c.FullName ?? $"{c.FirstName} {c.LastName}".Trim());
            csv.WriteField(c.Email);
            csv.WriteField(c.Phone);
            csv.WriteField(c.Organization);
            csv.WriteField(c.Title);
            csv.WriteField(c.Address);
            await csv.NextRecordAsync();
        }

        await writer.FlushAsync(ct);
        return TypedResults.File(ms.ToArray(), "text/csv", $"outlook_kontakter_{session.Id:N}.csv");
    }
}
