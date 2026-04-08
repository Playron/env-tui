namespace ContactExtractor.Api.AI;

public static class LlmNameNormalizationPrompt
{
    public static string Build(List<string> rawNames) =>
        $$"""
        Du er en ekspert på å normalisere personnavnformater til fornavn og etternavn.

        Normaliser følgende navn til standardformat. Returner KUN gyldig JSON, ingen forklaring.

        Eksempler på normalisering:
        - "HANSEN, OLA NORDMANN" → firstName: "Ola Nordmann", lastName: "Hansen"
        - "ole h." → firstName: "Ole", lastName: "H."
        - "Dr. Kari Nordmann, PhD" → firstName: "Kari", lastName: "Nordmann", title: "Dr."
        - "NORDMANN OLA" → firstName: "Ola", lastName: "Nordmann"

        Navn som skal normaliseres:
        {{string.Join("\n", rawNames.Select((n, i) => $"{i + 1}. {n}"))}}

        Returner JSON på dette formatet:
        {
          "results": [
            {
              "rawName": "HANSEN, OLA NORDMANN",
              "firstName": "Ola Nordmann",
              "lastName": "Hansen",
              "title": null
            }
          ]
        }

        VIKTIG: Ikke fabriker informasjon. Hvis du er usikker, behold originalen.
        """;
}
