namespace ContactExtractor.Api.Services;

public interface IFileParser
{
    bool CanParse(string fileExtension);
    Task<List<Contact>> ParseAsync(Stream fileStream, string fileName, CancellationToken ct = default);
    Task<PreviewResultDto> PreviewAsync(Stream fileStream, string fileName, CancellationToken ct = default);
}
