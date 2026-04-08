using System.Text.Json;
using System.Text.Json.Serialization;

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

    public async Task<Dictionary<string, NormalizedName>> NormalizeNamesAsync(
        List<string> rawNames, CancellationToken ct)
    {
        var prompt = LlmNameNormalizationPrompt.Build(rawNames);
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
        return ParseNormalizationResult(ollamaResponse!.Response);
    }

    private static Dictionary<string, NormalizedName> ParseNormalizationResult(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var results = doc.RootElement.GetProperty("results");
            var dict = new Dictionary<string, NormalizedName>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in results.EnumerateArray())
            {
                var raw   = item.TryGetProperty("rawName", out var rn) ? rn.GetString() : null;
                var first = item.TryGetProperty("firstName", out var fn) ? fn.GetString() : null;
                var last  = item.TryGetProperty("lastName", out var ln) ? ln.GetString() : null;
                var title = item.TryGetProperty("title", out var ti) ? ti.GetString() : null;
                if (raw is not null)
                    dict[raw] = new NormalizedName(first, last, title);
            }
            return dict;
        }
        catch { return []; }
    }
}

file record OllamaApiResponse(
    [property: JsonPropertyName("response")] string Response);
