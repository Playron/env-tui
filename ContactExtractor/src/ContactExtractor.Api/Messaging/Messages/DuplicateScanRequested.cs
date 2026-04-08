namespace ContactExtractor.Api.Messaging.Messages;

public record DuplicateScanRequested(
    Guid SessionId,
    string UserId);
