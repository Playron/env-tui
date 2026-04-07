using ContactExtractor.Api.AI.Providers;

namespace ContactExtractor.Api.AI;

public static class LlmProviderFactory
{
    public static IServiceCollection AddLlmService(
        this IServiceCollection services, IConfiguration config)
    {
        services.Configure<LlmSettings>(config.GetSection("Llm"));

        var settings = config.GetSection("Llm").Get<LlmSettings>();

        // If no LLM config, register a no-op service
        if (settings is null || string.IsNullOrWhiteSpace(settings.Provider))
        {
            services.AddScoped<ILlmService, NoOpLlmService>();
            return services;
        }

        switch (settings.Provider.ToLowerInvariant())
        {
            case "claude" or "anthropic":
                services.AddHttpClient<ILlmService, ClaudeService>();
                break;
            case "openai":
                services.AddHttpClient<ILlmService, OpenAiService>();
                break;
            case "ollama":
                services.AddHttpClient<ILlmService, OllamaService>();
                break;
            case "none" or "disabled":
                services.AddScoped<ILlmService, NoOpLlmService>();
                break;
            default:
                throw new InvalidOperationException(
                    $"Ukjent LLM-provider: '{settings.Provider}'. Bruk 'claude', 'openai', 'ollama' eller 'none'.");
        }

        return services;
    }
}

/// <summary>No-op implementation used when AI is disabled or not configured.</summary>
internal class NoOpLlmService : ILlmService
{
    public Task<LlmExtractionResult> ExtractContactsAsync(
        string rawText, string? fileContext, CancellationToken ct) =>
        Task.FromResult(new LlmExtractionResult([], "AI er ikke konfigurert.", 0));
}
