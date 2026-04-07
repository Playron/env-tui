using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace ContactExtractor.Api.AI.Providers;

public class ClaudeService(HttpClient httpClient, IOptions<LlmSettings> settings) : ILlmService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<LlmExtractionResult> ExtractContactsAsync(
        string rawText, string? fileContext, CancellationToken ct)
    {
        var prompt = LlmContactExtractionPrompt.Build(rawText, fileContext);
        var config = settings.Value;

        var request = new
        {
            model = config.Model ?? "claude-sonnet-4-5",
            max_tokens = config.MaxTokens,
            messages = new[] { new { role = "user", content = prompt } }
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
        req.Headers.Add("x-api-key", config.ApiKey ?? throw new InvalidOperationException("Llm:ApiKey er ikke konfigurert."));
        req.Headers.Add("anthropic-version", "2023-06-01");
        req.Content = JsonContent.Create(request);

        var response = await httpClient.SendAsync(req, ct);
        response.EnsureSuccessStatusCode();

        var claudeResponse = await response.Content.ReadFromJsonAsync<ClaudeApiResponse>(JsonOptions, ct);
        var json = claudeResponse!.Content.First(c => c.Type == "text").Text;

        // Strip potential markdown code fences
        json = StripMarkdownJson(json);

        return JsonSerializer.Deserialize<LlmExtractionResult>(json, JsonOptions)
            ?? new LlmExtractionResult([], "Tomt svar fra LLM", 0);
    }

    private static string StripMarkdownJson(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.StartsWith("```json"))
            trimmed = trimmed[7..];
        else if (trimmed.StartsWith("```"))
            trimmed = trimmed[3..];
        if (trimmed.EndsWith("```"))
            trimmed = trimmed[..^3];
        return trimmed.Trim();
    }
}

file record ClaudeApiResponse(
    [property: JsonPropertyName("content")] List<ClaudeContent> Content);

file record ClaudeContent(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("text")] string Text);
