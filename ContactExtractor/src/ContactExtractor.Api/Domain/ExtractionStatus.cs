namespace ContactExtractor.Api.Domain;

public enum ExtractionStatus
{
    Pending,       // Fil mottatt, venter i kø
    Extracting,    // Regex-ekstraksjon pågår
    AiProcessing,  // LLM-kall pågår
    Completed,     // Ferdig
    Failed         // Feilet
}
