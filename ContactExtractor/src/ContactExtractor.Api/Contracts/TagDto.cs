namespace ContactExtractor.Api.Contracts;

public record TagDto(Guid Id, string Name, string? Color);

public record CreateTagDto(string Name, string? Color = null);

public record UpdateTagDto(string? Name, string? Color);

public record BulkTagDto(List<Guid> ContactIds, Guid TagId);
