namespace ContactExtractor.Api.Messaging.Messages;

public record ExtractionRequested(
    Guid SessionId,
    string FilePath,          // Midlertidig lagret fil på disk
    string FileName,
    string FileExtension);
