namespace ContactExtractor.Api.Services.Integrations;

public class HubSpotExporter(HttpClient httpClient) : ICrmExporter
{
    public string Name => "HubSpot";

    public async Task<CrmExportResult> ExportAsync(
        List<Contact> contacts,
        string apiKey,
        CancellationToken ct = default)
    {
        // HubSpot Contacts API v3 – batch create
        var inputs = contacts.Select(c => new
        {
            properties = new Dictionary<string, string?>
            {
                ["firstname"] = c.FirstName,
                ["lastname"]  = c.LastName,
                ["email"]     = c.Email,
                ["phone"]     = c.Phone,
                ["company"]   = c.Organization,
                ["jobtitle"]  = c.Title,
                ["address"]   = c.Address
            }.Where(kv => kv.Value is not null)
             .ToDictionary(kv => kv.Key, kv => kv.Value)
        }).ToList();

        var payload = new { inputs };

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "https://api.hubapi.com/crm/v3/objects/contacts/batch/create");
        request.Headers.Add("Authorization", $"Bearer {apiKey}");
        request.Content = JsonContent.Create(payload);

        try
        {
            var response = await httpClient.SendAsync(request, ct);
            if (response.IsSuccessStatusCode)
                return new CrmExportResult(true, contacts.Count);

            var error = await response.Content.ReadAsStringAsync(ct);
            return new CrmExportResult(false, 0, $"HubSpot API feil: {error[..Math.Min(error.Length, 200)]}");
        }
        catch (Exception ex)
        {
            return new CrmExportResult(false, 0, ex.Message);
        }
    }
}
