namespace ContactExtractor.Api.Contracts;

public record DuplicateGroupDto(
    Guid Id,
    double Similarity,
    bool Resolved,
    List<ContactDto> Contacts);

public record MergeContactsDto(
    Guid PrimaryContactId,
    ContactUpdateDto? OverrideFields = null);
