using ContactExtractor.Api.AI;
using ContactExtractor.Api.Domain;
using ContactExtractor.Api.Domain.ValueObjects;
using ContactExtractor.Api.Services;
using ContactExtractor.Api.Services.Parsers;
using System.Text;
using System.Text.Json;

namespace ContactExtractor.Tests;

public class EmailAddressTests
{
    [Theory]
    [InlineData("test@example.com", true)]
    [InlineData("user.name+tag@sub.domain.no", true)]
    [InlineData("invalid", false)]
    [InlineData("no-at-sign.com", false)]
    [InlineData("@nodomain", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValid_ReturnsExpectedResult(string? email, bool expected)
    {
        EmailAddress.IsValid(email).ShouldBe(expected);
    }

    [Fact]
    public void Constructor_NormalizesToLowercase()
    {
        var email = new EmailAddress("TEST@EXAMPLE.COM");
        email.Value.ShouldBe("test@example.com");
    }

    [Fact]
    public void Constructor_ThrowsOnInvalidEmail()
    {
        var act = () => new EmailAddress("not-an-email");
        var ex = Should.Throw<ArgumentException>(act);
        ex.Message.ShouldContain("Ugyldig e-postadresse");
    }

    [Fact]
    public void TryCreate_ReturnsNullForInvalid()
    {
        EmailAddress.TryCreate("invalid").ShouldBeNull();
    }

    [Fact]
    public void TryCreate_ReturnsEmailAddressForValid()
    {
        var result = EmailAddress.TryCreate("user@example.com");
        result.ShouldNotBeNull();
        result!.Value.ShouldBe("user@example.com");
    }
}

public class PhoneNumberTests
{
    [Theory]
    [InlineData("99887766", true)]
    [InlineData("+4799887766", true)]
    [InlineData("1234567", true)]
    [InlineData("abc", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValid_ReturnsExpectedResult(string? phone, bool expected)
    {
        PhoneNumber.IsValid(phone).ShouldBe(expected);
    }

    [Fact]
    public void Constructor_DetectsNorwegianCountryCode()
    {
        var phone = new PhoneNumber("+4799887766");
        phone.CountryCode.ShouldBe("+47");
    }

    [Fact]
    public void Constructor_StripsNonDigits()
    {
        var phone = new PhoneNumber("99 88 77 66");
        phone.Value.ShouldBe("99887766");
    }

    [Fact]
    public void TryCreate_ReturnsNullForShortNumber()
    {
        PhoneNumber.TryCreate("123").ShouldBeNull();
    }
}

public class ColumnMappingTests
{
    private readonly ContactExtractionService _service = new();

    [Theory]
    [InlineData("e-post", "Email")]
    [InlineData("Email", "Email")]
    [InlineData("epost", "Email")]
    [InlineData("fornavn", "FirstName")]
    [InlineData("firstname", "FirstName")]
    [InlineData("etternavn", "LastName")]
    [InlineData("telefon", "Phone")]
    [InlineData("mobil", "Phone")]
    [InlineData("firma", "Organization")]
    [InlineData("adresse", "Address")]
    public void DetectColumnMapping_RecognizesKnownHeaders(string header, string expected)
    {
        var result = _service.DetectColumnMapping(header, []);
        result.ShouldBe(expected);
    }

    [Fact]
    public void DetectColumnMapping_FallsBackToContentDetection_ForEmail()
    {
        var samples = new[] { "test@example.com", "user@domain.no", "another@mail.org" };
        var result = _service.DetectColumnMapping("Kolonne1", samples);
        result.ShouldBe("Email");
    }

    [Fact]
    public void DetectColumnMapping_FallsBackToContentDetection_ForPhone()
    {
        var samples = new[] { "99887766", "12345678", "87654321" };
        var result = _service.DetectColumnMapping("Kolonne2", samples);
        result.ShouldBe("Phone");
    }
}

public class TextExtractionTests
{
    private readonly ContactExtractionService _service = new();

    [Fact]
    public void ExtractFromText_FindsEmailAndPhone()
    {
        var text = """
            John Doe
            john.doe@example.com
            +47 99 88 77 66
            """;

        var contacts = _service.ExtractFromText(text, Guid.Empty);

        contacts.ShouldNotBeEmpty();
        contacts.ShouldContain(c => c.Email == "john.doe@example.com");
    }

    [Fact]
    public void ExtractFromText_HandlesMultipleContacts()
    {
        var text = """
            Alice Smith - alice@test.com - 12345678
            Bob Jones - bob@test.com - 87654321
            """;

        var contacts = _service.ExtractFromText(text, Guid.Empty);
        contacts.Count.ShouldBeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public void ExtractFromText_ReturnsEmptyForTextWithNoContacts()
    {
        var text = "Dette er bare en vanlig tekst uten kontaktinformasjon.";
        var contacts = _service.ExtractFromText(text, Guid.Empty);
        contacts.ShouldBeEmpty();
    }

    [Fact]
    public void ExtractFromText_SetsExtractionSourceToRegex()
    {
        var text = "Alice alice@example.com";
        var contacts = _service.ExtractFromText(text, Guid.Empty);
        contacts.ShouldAllBe(c => c.ExtractionSource == "regex");
    }
}

public class VCardParserTests
{
    private readonly VCardParser _parser = new();

    [Fact]
    public async Task ParseAsync_ParsesSingleVCard()
    {
        var vcf = """
            BEGIN:VCARD
            VERSION:3.0
            FN:Ola Nordmann
            N:Nordmann;Ola;;;
            EMAIL:ola@example.no
            TEL:+4799887766
            ORG:Eksempel AS
            END:VCARD
            """;

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(vcf));
        var contacts = await _parser.ParseAsync(stream, "test.vcf");

        contacts.Count.ShouldBe(1);
        var c = contacts[0];
        c.FullName.ShouldBe("Ola Nordmann");
        c.FirstName.ShouldBe("Ola");
        c.LastName.ShouldBe("Nordmann");
        c.Email.ShouldBe("ola@example.no");
        c.Organization.ShouldBe("Eksempel AS");
        c.ExtractionSource.ShouldBe("regex");
    }

    [Fact]
    public async Task ParseAsync_ParsesMultipleVCards()
    {
        var vcf = """
            BEGIN:VCARD
            VERSION:3.0
            FN:Alice
            EMAIL:alice@example.com
            END:VCARD
            BEGIN:VCARD
            VERSION:3.0
            FN:Bob
            EMAIL:bob@example.com
            END:VCARD
            """;

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(vcf));
        var contacts = await _parser.ParseAsync(stream, "test.vcf");
        contacts.Count.ShouldBe(2);
    }
}

public class LlmPromptTests
{
    [Fact]
    public void Build_ContainsRawText()
    {
        var text = "Ola Nordmann, ola@example.no";
        var prompt = LlmContactExtractionPrompt.Build(text, null);
        prompt.ShouldContain(text);
    }

    [Fact]
    public void Build_ContainsFileContextWhenProvided()
    {
        var prompt = LlmContactExtractionPrompt.Build("some text", "PDF-fil: test.pdf");
        prompt.ShouldContain("PDF-fil: test.pdf");
    }

    [Fact]
    public void Build_DoesNotContainContextWhenNull()
    {
        var prompt = LlmContactExtractionPrompt.Build("some text", null);
        prompt.ShouldNotContain("Kontekst om filen:");
    }

    [Fact]
    public void Build_IncludesJsonSchemaInstructions()
    {
        var prompt = LlmContactExtractionPrompt.Build("test", null);
        prompt.ShouldContain("\"contacts\"");
        prompt.ShouldContain("\"email\"");
        prompt.ShouldContain("\"phone\"");
        prompt.ShouldContain("overallConfidence");
    }
}

public class LlmExtractionResultDeserializationTests
{
    [Fact]
    public void Deserialize_ValidJson_ReturnsCorrectResult()
    {
        var json = """
            {
              "contacts": [
                {
                  "firstName": "Ola",
                  "lastName": "Nordmann",
                  "fullName": "Ola Nordmann",
                  "email": "ola@example.no",
                  "phone": "+4799887766",
                  "organization": "Eksempel AS",
                  "title": "Daglig leder",
                  "address": "Storgata 1, Oslo"
                }
              ],
              "reasoning": "Fant én kontakt i teksten",
              "overallConfidence": 0.95
            }
            """;

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var result = JsonSerializer.Deserialize<LlmExtractionResult>(json, options);

        result.ShouldNotBeNull();
        result!.Contacts.Count.ShouldBe(1);
        result.OverallConfidence.ShouldBe(0.95);
        result.Contacts[0].Email.ShouldBe("ola@example.no");
        result.Contacts[0].Organization.ShouldBe("Eksempel AS");
        result.Reasoning.ShouldContain("Fant én kontakt");
    }

    [Fact]
    public void Deserialize_EmptyContacts_ReturnsEmptyList()
    {
        var json = """
            {
              "contacts": [],
              "reasoning": "Ingen kontakter funnet",
              "overallConfidence": 0.1
            }
            """;

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var result = JsonSerializer.Deserialize<LlmExtractionResult>(json, options);

        result!.Contacts.ShouldBeEmpty();
        result.OverallConfidence.ShouldBe(0.1);
    }

    [Fact]
    public void Deserialize_NullFields_HandledGracefully()
    {
        var json = """
            {
              "contacts": [
                {
                  "firstName": null,
                  "lastName": null,
                  "fullName": "Kari Nordmann",
                  "email": null,
                  "phone": null,
                  "organization": null,
                  "title": null,
                  "address": null
                }
              ],
              "reasoning": null,
              "overallConfidence": 0.5
            }
            """;

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var result = JsonSerializer.Deserialize<LlmExtractionResult>(json, options);

        result!.Contacts[0].FullName.ShouldBe("Kari Nordmann");
        result.Contacts[0].Email.ShouldBeNull();
        result.Reasoning.ShouldBeNull();
    }
}
