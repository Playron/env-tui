namespace ContactExtractor.Api.AI;

public class LlmSettings
{
    public required string Provider { get; init; }   // "claude", "openai", "ollama"
    public string? ApiKey { get; init; }
    public string? Model { get; init; }              // Optional model override
    public string? BaseUrl { get; init; }            // For Ollama
    public int MaxTokens { get; init; } = 4096;
    public int MaxInputCharacters { get; init; } = 50_000;
}
