namespace ContactExtractor.Api.Services;

public class FileParserFactory(IEnumerable<IFileParser> parsers)
{
    public IFileParser? GetParser(string extension) =>
        parsers.FirstOrDefault(p => p.CanParse(extension.ToLowerInvariant()));

    public IReadOnlyList<string> SupportedExtensions =>
        parsers.SelectMany(p => new[] { ".csv", ".xlsx", ".pdf", ".docx", ".txt", ".vcf" })
               .Distinct()
               .ToList();
}
