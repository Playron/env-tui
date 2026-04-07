namespace ContactExtractor.Api.Contracts;

public record ColumnMappingDto(
    string SourceColumn,
    string? MappedTo,
    string[] SampleValues);

public record ColumnMappingUpdateDto(
    List<ColumnMappingDto> Mappings);

public record SupportedFormatDto(
    string Extension,
    string Description,
    string Icon);
