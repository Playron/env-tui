using ContactExtractor.Api.AI;
using ContactExtractor.Api.Contracts;
using ContactExtractor.Api.Services;
using ContactExtractor.Api.Services.Parsers;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Extensions.Options;
using OfficeOpenXml;
using System.Text;

namespace ContactExtractor.Tests;

// ---------------------------------------------------------------------------
// Shared test helpers
// ---------------------------------------------------------------------------

file sealed class FakeLlmService : ILlmService
{
    public LlmExtractionResult Result { get; set; } =
        new([], null, 0);

    public bool ShouldThrow { get; set; }

    public Task<LlmExtractionResult> ExtractContactsAsync(
        string rawText, string? fileContext = null, CancellationToken ct = default)
    {
        if (ShouldThrow) throw new InvalidOperationException("LLM unavailable");
        return Task.FromResult(Result);
    }

    public Task<Dictionary<string, NormalizedName>> NormalizeNamesAsync(
        List<string> rawNames, CancellationToken ct = default) =>
        Task.FromResult(new Dictionary<string, NormalizedName>());
}

// ---------------------------------------------------------------------------
// ExcelParser tests
// ---------------------------------------------------------------------------

public class ExcelParserTests
{
    private readonly ContactExtractionService _svc = new();

    private ExcelParser CreateParser() => new(_svc);

    /// Creates an in-memory .xlsx stream via EPPlus.
    private static MemoryStream CreateXlsxStream(Action<ExcelWorksheet> configure)
    {
        ExcelPackage.License.SetNonCommercialPersonal("ContactExtractor");
        using var pkg = new ExcelPackage();
        var sheet = pkg.Workbook.Worksheets.Add("Sheet1");
        configure(sheet);
        var ms = new MemoryStream();
        pkg.SaveAs(ms);
        ms.Position = 0;
        return ms;
    }

    [Fact]
    public void CanParse_ReturnsTrueForXlsx() =>
        CreateParser().CanParse(".xlsx").ShouldBeTrue();

    [Fact]
    public void CanParse_ReturnsTrueForXls() =>
        CreateParser().CanParse(".xls").ShouldBeTrue();

    [Fact]
    public void CanParse_ReturnsFalseForOtherExtensions()
    {
        var p = CreateParser();
        p.CanParse(".csv").ShouldBeFalse();
        p.CanParse(".pdf").ShouldBeFalse();
        p.CanParse(".docx").ShouldBeFalse();
    }

    [Fact]
    public async Task ParseAsync_WithEnglishHeaders_ExtractsContacts()
    {
        using var stream = CreateXlsxStream(s =>
        {
            s.Cells[1, 1].Value = "Name";
            s.Cells[1, 2].Value = "Email";
            s.Cells[1, 3].Value = "Phone";
            s.Cells[2, 1].Value = "Alice Smith";
            s.Cells[2, 2].Value = "alice@example.com";
            s.Cells[2, 3].Value = "+4799887766";
            s.Cells[3, 1].Value = "Bob Jones";
            s.Cells[3, 2].Value = "bob@example.com";
            s.Cells[3, 3].Value = "12345678";
        });

        var contacts = await CreateParser().ParseAsync(stream, "contacts.xlsx");

        contacts.Count.ShouldBe(2);
        contacts.ShouldContain(c => c.Email == "alice@example.com");
        contacts.ShouldContain(c => c.Email == "bob@example.com");
        contacts[0].Phone.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task ParseAsync_WithNorwegianHeaders_ExtractsContacts()
    {
        using var stream = CreateXlsxStream(s =>
        {
            s.Cells[1, 1].Value = "fornavn";
            s.Cells[1, 2].Value = "etternavn";
            s.Cells[1, 3].Value = "e-post";
            s.Cells[1, 4].Value = "telefon";
            s.Cells[2, 1].Value = "Ola";
            s.Cells[2, 2].Value = "Nordmann";
            s.Cells[2, 3].Value = "ola@example.no";
            s.Cells[2, 4].Value = "99887766";
        });

        var contacts = await CreateParser().ParseAsync(stream, "kontakter.xlsx");

        contacts.Count.ShouldBe(1);
        var c = contacts[0];
        c.FirstName.ShouldBe("Ola");
        c.LastName.ShouldBe("Nordmann");
        c.Email.ShouldBe("ola@example.no");
        c.Phone.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task ParseAsync_WithOrganizationAndTitle_ExtractsAllFields()
    {
        using var stream = CreateXlsxStream(s =>
        {
            s.Cells[1, 1].Value = "name";
            s.Cells[1, 2].Value = "email";
            s.Cells[1, 3].Value = "firma";
            s.Cells[1, 4].Value = "tittel";
            s.Cells[2, 1].Value = "Kari Nordmann";
            s.Cells[2, 2].Value = "kari@bedrift.no";
            s.Cells[2, 3].Value = "Norsk AS";
            s.Cells[2, 4].Value = "CEO";
        });

        var contacts = await CreateParser().ParseAsync(stream, "test.xlsx");

        contacts.Count.ShouldBe(1);
        contacts[0].Organization.ShouldBe("Norsk AS");
        contacts[0].Title.ShouldBe("CEO");
    }

    [Fact]
    public async Task ParseAsync_SetsConfidenceTo09()
    {
        using var stream = CreateXlsxStream(s =>
        {
            s.Cells[1, 1].Value = "Email";
            s.Cells[2, 1].Value = "test@example.com";
        });

        var contacts = await CreateParser().ParseAsync(stream, "test.xlsx");

        contacts.Count.ShouldBe(1);
        contacts[0].Confidence.ShouldBe(0.9);
    }

    [Fact]
    public async Task ParseAsync_WithEmptySheet_ReturnsEmpty()
    {
        using var stream = CreateXlsxStream(_ => { });
        var contacts = await CreateParser().ParseAsync(stream, "empty.xlsx");
        contacts.ShouldBeEmpty();
    }

    [Fact]
    public async Task ParseAsync_SkipsRowsWithNoMeaningfulData()
    {
        // Only 'address' column - HasAnyData requires name/email/phone
        using var stream = CreateXlsxStream(s =>
        {
            s.Cells[1, 1].Value = "adresse";
            s.Cells[2, 1].Value = "Storgata 1, Oslo";
        });

        var contacts = await CreateParser().ParseAsync(stream, "test.xlsx");
        contacts.ShouldBeEmpty();
    }

    [Fact]
    public async Task ParseAsync_WithMultipleRows_ExtractsAll()
    {
        using var stream = CreateXlsxStream(s =>
        {
            s.Cells[1, 1].Value = "Email";
            s.Cells[2, 1].Value = "a@example.com";
            s.Cells[3, 1].Value = "b@example.com";
            s.Cells[4, 1].Value = "c@example.com";
        });

        var contacts = await CreateParser().ParseAsync(stream, "test.xlsx");
        contacts.Count.ShouldBe(3);
    }

    [Fact]
    public async Task PreviewAsync_ReturnsHeadersAndSampleRows()
    {
        using var stream = CreateXlsxStream(s =>
        {
            s.Cells[1, 1].Value = "Name";
            s.Cells[1, 2].Value = "Email";
            s.Cells[2, 1].Value = "Alice";
            s.Cells[2, 2].Value = "alice@example.com";
            s.Cells[3, 1].Value = "Bob";
            s.Cells[3, 2].Value = "bob@example.com";
        });

        var preview = await CreateParser().PreviewAsync(stream, "contacts.xlsx");

        preview.Headers.ShouldContain("Name");
        preview.Headers.ShouldContain("Email");
        preview.SampleRows.Count.ShouldBe(2);
        preview.SuggestedMappings.ShouldContain(m => m.MappedTo == "Email");
    }

    [Fact]
    public async Task PreviewAsync_WithEmptySheet_ReturnsEmptyPreview()
    {
        using var stream = CreateXlsxStream(_ => { });
        var preview = await CreateParser().PreviewAsync(stream, "empty.xlsx");

        preview.Headers.ShouldBeEmpty();
        preview.SampleRows.ShouldBeEmpty();
    }
}

// ---------------------------------------------------------------------------
// PdfParser tests
// ---------------------------------------------------------------------------

public class PdfParserTests
{
    private readonly ContactExtractionService _svc = new();
    private readonly IOptions<LlmSettings> _settings =
        Options.Create(new LlmSettings { Provider = "claude", MaxInputCharacters = 50_000 });

    private PdfParser CreateParser(ILlmService? llm = null) =>
        new(_svc, llm ?? new FakeLlmService(), _settings);

    /// Builds a minimal valid PDF byte array containing the given text lines.
    /// Uses standard Type1 Helvetica font with WinAnsiEncoding (ASCII-safe).
    private static MemoryStream CreatePdfStream(params string[] lines)
    {
        // Content stream
        var cs = new StringBuilder();
        cs.Append("BT\n/F1 12 Tf\n72 720 Td\n");
        for (var i = 0; i < lines.Length; i++)
        {
            if (i > 0) cs.Append("0 -14 Td\n");
            var safe = lines[i]
                .Replace("\\", "\\\\")
                .Replace("(", "\\(")
                .Replace(")", "\\)");
            cs.Append($"({safe}) Tj\n");
        }
        cs.Append("ET\n");
        var streamContent = cs.ToString();
        var streamLen = streamContent.Length; // all ASCII

        // PDF object strings
        var header = "%PDF-1.4\n";
        var o1 = "1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n";
        var o2 = "2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n";
        var o3 = "3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792]" +
                 " /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>\nendobj\n";
        var o4h = $"4 0 obj\n<< /Length {streamLen} >>\nstream\n";
        var o4f = "endstream\nendobj\n";
        var o5 = "5 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica" +
                 " /Encoding /WinAnsiEncoding >>\nendobj\n";

        // Byte offsets for xref table
        var off1 = header.Length;
        var off2 = off1 + o1.Length;
        var off3 = off2 + o2.Length;
        var off4 = off3 + o3.Length;
        var off5 = off4 + o4h.Length + streamLen + o4f.Length;
        var xrefOff = off5 + o5.Length;

        var xref = new StringBuilder();
        xref.Append("xref\n0 6\n");
        xref.Append("0000000000 65535 f \n");
        xref.Append($"{off1:D10} 00000 n \n");
        xref.Append($"{off2:D10} 00000 n \n");
        xref.Append($"{off3:D10} 00000 n \n");
        xref.Append($"{off4:D10} 00000 n \n");
        xref.Append($"{off5:D10} 00000 n \n");
        xref.Append($"trailer\n<< /Size 6 /Root 1 0 R >>\nstartxref\n{xrefOff}\n%%EOF\n");

        var ms = new MemoryStream();
        // Use ASCII encoding — all characters are in ASCII range
        var enc = new ASCIIEncoding();
        var write = (string s) => { var b = enc.GetBytes(s); ms.Write(b, 0, b.Length); };
        write(header);
        write(o1); write(o2); write(o3);
        write(o4h); write(streamContent); write(o4f);
        write(o5);
        write(xref.ToString());
        ms.Position = 0;
        return ms;
    }

    [Fact]
    public void CanParse_ReturnsTrueForPdf() =>
        CreateParser().CanParse(".pdf").ShouldBeTrue();

    [Fact]
    public void CanParse_ReturnsFalseForOtherExtensions()
    {
        var p = CreateParser();
        p.CanParse(".xlsx").ShouldBeFalse();
        p.CanParse(".docx").ShouldBeFalse();
    }

    [Fact]
    public async Task ParseAsync_WithTwoContacts_ExtractsViaRegex()
    {
        // Two contacts => regex confidence sufficient, no AI call needed
        using var stream = CreatePdfStream(
            "Alice Smith",
            "alice@example.com",
            "+4799887766",
            "Bob Jones",
            "bob@example.com",
            "+4712345678");

        var contacts = await CreateParser().ParseAsync(stream, "test.pdf");

        contacts.Count.ShouldBeGreaterThanOrEqualTo(2);
        contacts.ShouldContain(c => c.Email == "alice@example.com");
        contacts.ShouldContain(c => c.Email == "bob@example.com");
    }

    [Fact]
    public async Task ParseAsync_SetsExtractionSourceToRegex_WhenRegexSuffices()
    {
        using var stream = CreatePdfStream(
            "Alice Smith", "alice@example.com", "+4799887766",
            "Bob Jones", "bob@example.com", "+4712345678");

        var contacts = await CreateParser().ParseAsync(stream, "test.pdf");

        contacts.ShouldAllBe(c => c.ExtractionSource == "regex");
    }

    [Fact]
    public async Task ParseAsync_FallsBackToAi_WhenRegexFindsOnlyOneContact()
    {
        // One contact => ShouldUseAi returns true
        var fakeLlm = new FakeLlmService
        {
            Result = new LlmExtractionResult(
            [
                new LlmContact("Kari", "Nordmann", "Kari Nordmann",
                               "kari@example.no", null, null, null, null)
            ], "found via AI", 0.9)
        };

        using var stream = CreatePdfStream("Kari Nordmann", "kari@example.no");
        var contacts = await CreateParser(fakeLlm).ParseAsync(stream, "test.pdf");

        contacts.ShouldContain(c => c.Email == "kari@example.no");
    }

    [Fact]
    public async Task ParseAsync_ReturnsRegexResults_WhenAiFails()
    {
        var failingLlm = new FakeLlmService { ShouldThrow = true };

        // One contact triggers AI path, but AI fails — regex results should be returned
        using var stream = CreatePdfStream("Jane Doe", "jane@example.com");
        var contacts = await CreateParser(failingLlm).ParseAsync(stream, "test.pdf");

        contacts.ShouldContain(c => c.Email == "jane@example.com");
        contacts.ShouldAllBe(c => c.ExtractionSource == "regex");
    }

    [Fact]
    public async Task ParseAsync_WithEmptyPdf_ReturnsEmpty()
    {
        using var stream = CreatePdfStream(); // no text lines
        var contacts = await CreateParser().ParseAsync(stream, "empty.pdf");
        contacts.ShouldBeEmpty();
    }

    [Fact]
    public async Task PreviewAsync_ReturnsUpToFiveLines()
    {
        using var stream = CreatePdfStream(
            "Line 1", "Line 2", "Line 3", "Line 4", "Line 5", "Line 6", "Line 7");

        var preview = await CreateParser().PreviewAsync(stream, "test.pdf");

        preview.SampleRows.Count.ShouldBe(5);
        preview.Headers.ShouldContain("text");
        preview.FileType.ShouldBe(".pdf");
    }

    [Fact]
    public async Task ParseWithoutAiAsync_ReturnsContactsAndRawText()
    {
        using var stream = CreatePdfStream(
            "Alice Smith", "alice@example.com", "+4799887766",
            "Bob Jones", "bob@example.com", "+4712345678");

        var (contacts, rawText) = await CreateParser()
            .ParseWithoutAiAsync(stream, "test.pdf");

        rawText.ShouldContain("alice@example.com");
        contacts.ShouldContain(c => c.Email == "alice@example.com");
        contacts.ShouldAllBe(c => c.ExtractionSource == "regex");
    }
}

// ---------------------------------------------------------------------------
// WordParser tests
// ---------------------------------------------------------------------------

public class WordParserTests
{
    private readonly ContactExtractionService _svc = new();
    private readonly IOptions<LlmSettings> _settings =
        Options.Create(new LlmSettings { Provider = "claude", MaxInputCharacters = 50_000 });

    private WordParser CreateParser(ILlmService? llm = null) =>
        new(_svc, llm ?? new FakeLlmService(), _settings);

    /// Creates an in-memory .docx stream with one paragraph per line.
    private static MemoryStream CreateDocxStream(params string[] paragraphs)
    {
        var ms = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document))
        {
            var main = doc.AddMainDocumentPart();
            var body = new Body();
            foreach (var text in paragraphs)
                body.AppendChild(new Paragraph(new Run(new Text(text))));
            main.Document = new Document(body);
            main.Document.Save();
        }
        ms.Position = 0;
        return ms;
    }

    [Fact]
    public void CanParse_ReturnsTrueForDocx() =>
        CreateParser().CanParse(".docx").ShouldBeTrue();

    [Fact]
    public void CanParse_ReturnsFalseForOtherExtensions()
    {
        var p = CreateParser();
        p.CanParse(".pdf").ShouldBeFalse();
        p.CanParse(".xlsx").ShouldBeFalse();
        p.CanParse(".doc").ShouldBeFalse();
    }

    [Fact]
    public async Task ParseAsync_WithTwoContacts_ExtractsViaRegex()
    {
        using var stream = CreateDocxStream(
            "Alice Smith",
            "alice@example.com",
            "+4799887766",
            "Bob Jones",
            "bob@example.com",
            "+4712345678");

        var contacts = await CreateParser().ParseAsync(stream, "test.docx");

        contacts.Count.ShouldBeGreaterThanOrEqualTo(2);
        contacts.ShouldContain(c => c.Email == "alice@example.com");
        contacts.ShouldContain(c => c.Email == "bob@example.com");
    }

    [Fact]
    public async Task ParseAsync_SetsExtractionSourceToRegex_WhenRegexSuffices()
    {
        using var stream = CreateDocxStream(
            "Alice Smith", "alice@example.com", "+4799887766",
            "Bob Jones", "bob@example.com", "+4712345678");

        var contacts = await CreateParser().ParseAsync(stream, "test.docx");

        contacts.ShouldAllBe(c => c.ExtractionSource == "regex");
    }

    [Fact]
    public async Task ParseAsync_FallsBackToAi_WhenRegexFindsOnlyOneContact()
    {
        var fakeLlm = new FakeLlmService
        {
            Result = new LlmExtractionResult(
            [
                new LlmContact("Ola", "Nordmann", "Ola Nordmann",
                               "ola@example.no", null, null, null, null)
            ], "found via AI", 0.9)
        };

        using var stream = CreateDocxStream("Ola Nordmann", "ola@example.no");
        var contacts = await CreateParser(fakeLlm).ParseAsync(stream, "test.docx");

        contacts.ShouldContain(c => c.Email == "ola@example.no");
    }

    [Fact]
    public async Task ParseAsync_ReturnsRegexResults_WhenAiFails()
    {
        var failingLlm = new FakeLlmService { ShouldThrow = true };

        using var stream = CreateDocxStream("Jane Doe", "jane@example.com");
        var contacts = await CreateParser(failingLlm).ParseAsync(stream, "test.docx");

        contacts.ShouldContain(c => c.Email == "jane@example.com");
        contacts.ShouldAllBe(c => c.ExtractionSource == "regex");
    }

    [Fact]
    public async Task ParseAsync_WithEmptyDocument_ReturnsEmpty()
    {
        using var stream = CreateDocxStream(); // no paragraphs
        var contacts = await CreateParser().ParseAsync(stream, "empty.docx");
        contacts.ShouldBeEmpty();
    }

    [Fact]
    public async Task PreviewAsync_ReturnsUpToFiveLines()
    {
        using var stream = CreateDocxStream(
            "Line 1", "Line 2", "Line 3", "Line 4", "Line 5", "Line 6", "Line 7");

        var preview = await CreateParser().PreviewAsync(stream, "test.docx");

        preview.SampleRows.Count.ShouldBe(5);
        preview.Headers.ShouldContain("text");
        preview.FileType.ShouldBe(".docx");
    }

    [Fact]
    public async Task PreviewAsync_WithEmptyDocument_ReturnsEmptyRows()
    {
        using var stream = CreateDocxStream();
        var preview = await CreateParser().PreviewAsync(stream, "empty.docx");
        preview.SampleRows.ShouldBeEmpty();
    }

    [Fact]
    public async Task ParseWithoutAiAsync_ReturnsContactsAndRawText()
    {
        using var stream = CreateDocxStream(
            "Alice Smith", "alice@example.com", "+4799887766",
            "Bob Jones", "bob@example.com", "+4712345678");

        var (contacts, rawText) = await CreateParser()
            .ParseWithoutAiAsync(stream, "test.docx");

        rawText.ShouldContain("alice@example.com");
        contacts.ShouldContain(c => c.Email == "alice@example.com");
    }
}
