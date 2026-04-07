namespace ContactExtractor.Api.AI;

public static class LlmContactExtractionPrompt
{
    public static string Build(string rawText, string? fileContext) => $$"""
        Du er en ekspert på å ekstrahere kontaktinformasjon fra ustrukturert tekst.

        Analyser følgende tekst og ekstraher alle personer/kontakter du finner.
        For hver kontakt, ekstraher så mye som mulig av:
        - Fornavn (firstName)
        - Etternavn (lastName)
        - Fullt navn (fullName)
        - E-postadresse (email)
        - Telefonnummer (phone) – inkluder landskode hvis tilgjengelig
        - Organisasjon/firma (organization)
        - Tittel/stilling (title)
        - Adresse (address)

        Regler:
        - Returner KUN gyldig JSON, ingen annen tekst
        - Hvis et felt ikke finnes, bruk null
        - Norske telefonnumre har typisk 8 siffer, eventuelt med +47
        - Vær oppmerksom på norske navn og formater
        - Ikke gjett eller fabrikker data som ikke finnes i teksten

        {{(fileContext is not null ? $"Kontekst om filen: {fileContext}\n" : "")}}

        Tekst å analysere:
        ---
        {{rawText}}
        ---

        Svar med denne JSON-strukturen:
        {
          "contacts": [
            {
              "firstName": "...",
              "lastName": "...",
              "fullName": "...",
              "email": "...",
              "phone": "...",
              "organization": "...",
              "title": "...",
              "address": "..."
            }
          ],
          "reasoning": "Kort forklaring av hva du fant og eventuelle usikkerheter",
          "overallConfidence": 0.95
        }
        """;
}
