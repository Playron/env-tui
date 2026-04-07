namespace ContactExtractor.Api.Endpoints;

public static class SettingsEndpoints
{
    public static void MapSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/settings")
            .WithTags("Settings");

        group.MapGet("/llm", GetLlmSettings)
            .Produces<LlmSettingsInfoDto>(200)
            .WithSummary("Vis aktiv LLM-provider (uten API-nøkkel)");
    }

    private static Ok<LlmSettingsInfoDto> GetLlmSettings(IOptions<LlmSettings> settings)
    {
        var s = settings.Value;
        return TypedResults.Ok(new LlmSettingsInfoDto(
            s.Provider,
            s.Model,
            !string.IsNullOrWhiteSpace(s.ApiKey),
            s.BaseUrl));
    }
}
