using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace ContactExtractor.Api.AI.Providers;

public class OpenAiService(HttpClient httpClient, IOptions<LlmSettings> settings) : ILlmService
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
            model = config.Model ?? "gpt-4o",
            messages = new[] { new { role = "user", content = prompt } },
            response_format = new { type = "json_object" }
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
        req.Headers.Add("Authorization", $"Bearer {config.ApiKey ?? throw new InvalidOperationException("Llm:ApiKey er ikke konfigurert.")}");
        req.Content = JsonContent.Create(request);

        var response = await httpClient.SendAsync(req, ct);
        response.EnsureSuccessStatusCode();

        var openAiResponse = await response.Content.ReadFromJsonAsync<OpenAiApiResponse>(JsonOptions, ct);
        var json = openAiResponse!.Choices[0].Message.Content;

        return JsonSerializer.Deserialize<LlmExtractionResult>(json, JsonOptions)
            ?? new LlmExtractionResult([], "Tomt svar fra LLM", 0);
    }
}

file record OpenAiApiResponse(
    [property: JsonPropertyName("choices")] List<OpenAiChoice> Choices);

file record OpenAiChoice(
    [property: JsonPropertyName("message")] OpenAiMessage Message);

file record OpenAiMessage(
    [property: JsonPropertyName("content")] string Content);
