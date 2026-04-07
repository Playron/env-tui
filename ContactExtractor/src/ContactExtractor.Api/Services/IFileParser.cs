namespace ContactExtractor.Api.Services;

public interface IFileParser
{
    bool CanParse(string fileExtension);

    Task<List<Contact>> ParseAsync(Stream fileStream, string fileName, CancellationToken ct = default);

    Task<PreviewResultDto> PreviewAsync(Stream fileStream, string fileName, CancellationToken ct = default);

    // Brukes av den asynkrone consumer-path: kjører kun regex-ekstraksjon og returnerer råtekst.
    // AI-beslutningen tas av consumeren sentralt for å unngå dobbel LLM-kall.
    // Parsere uten AI (CSV, Excel, VCard) arver default-implementasjon.
    // Parsere med AI (PDF, Word, TXT) override denne.
    Task<(List<Contact> Contacts, string RawText)> ParseWithoutAiAsync(
        Stream fileStream, string fileName, CancellationToken ct = default)
        => ParseAsync(fileStream, fileName, ct)
            .ContinueWith(t => (t.Result, string.Empty), ct,
                TaskContinuationOptions.OnlyOnRanToCompletion,
                TaskScheduler.Default);
}
