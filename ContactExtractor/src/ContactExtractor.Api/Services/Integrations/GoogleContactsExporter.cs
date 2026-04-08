namespace ContactExtractor.Api.Services.Integrations;

public class GoogleContactsExporter(HttpClient httpClient) : ICrmExporter
{
    public string Name => "Google Contacts";

    public async Task<CrmExportResult> ExportAsync(
        List<Contact> contacts,
        string apiKey,
        CancellationToken ct = default)
    {
        // Google People API v1 – batch create
        var connections = contacts.Select(c => new
        {
            names = new[]
            {
                new
                {
                    givenName  = c.FirstName,
                    familyName = c.LastName,
                    displayName = c.FullName ?? $"{c.FirstName} {c.LastName}".Trim()
                }
            },
            emailAddresses = c.Email is not null
                ? new[] { new { value = c.Email } }
                : Array.Empty<object>(),
            phoneNumbers = c.Phone is not null
                ? new[] { new { value = c.Phone } }
                : Array.Empty<object>(),
            organizations = c.Organization is not null
                ? new[] { new { name = c.Organization, title = c.Title } }
                : Array.Empty<object>()
        }).ToList();

        var successCount = 0;
        foreach (var person in connections)
        {
            try
            {
                using var request = new HttpRequestMessage(
                    HttpMethod.Post,
                    $"https://people.googleapis.com/v1/people:createContact?access_token={apiKey}");
                request.Content = JsonContent.Create(person);
                var response = await httpClient.SendAsync(request, ct);
                if (response.IsSuccessStatusCode) successCount++;
            }
            catch
            {
                // Fortsett med neste kontakt ved feil
            }
        }

        return successCount > 0
            ? new CrmExportResult(true, successCount)
            : new CrmExportResult(false, 0, "Ingen kontakter ble eksportert til Google Contacts.");
    }
}
