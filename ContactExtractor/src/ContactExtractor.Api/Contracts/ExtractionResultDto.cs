namespace ContactExtractor.Api.Contracts;

public record ExtractionResultDto(
    Guid SessionId,
    string OriginalFileName,
    string FileType,
    int TotalRowsProcessed,
    int ContactsExtracted,
    bool UsedAi,
    List<ContactDto> Contacts,
    List<string> Warnings);

public record PreviewResultDto(
    string FileName,
    string FileType,
    List<string> Headers,
    List<Dictionary<string, string>> SampleRows,
    List<ColumnMappingDto> SuggestedMappings);

public record LlmSettingsInfoDto(
    string Provider,
    string? Model,
    bool HasApiKey,
    string? BaseUrl);
