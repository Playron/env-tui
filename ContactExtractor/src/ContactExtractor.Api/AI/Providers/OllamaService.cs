using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace ContactExtractor.Api.AI.Providers;

public class OllamaService(HttpClient httpClient, IOptions<LlmSettings> settings) : ILlmService
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
        var baseUrl = config.BaseUrl ?? "http://localhost:11434";

        var request = new
        {
            model = config.Model ?? "llama3.1",
            prompt,
            format = "json",
            stream = false
        };

        var response = await httpClient.PostAsJsonAsync($"{baseUrl}/api/generate", request, ct);
        response.EnsureSuccessStatusCode();

        var ollamaResponse = await response.Content.ReadFromJsonAsync<OllamaApiResponse>(JsonOptions, ct);

        return JsonSerializer.Deserialize<LlmExtractionResult>(ollamaResponse!.Response, JsonOptions)
            ?? new LlmExtractionResult([], "Tomt svar fra Ollama", 0);
    }
}

file record OllamaApiResponse(
    [property: JsonPropertyName("response")] string Response);
