using ContactExtractor.Api.Domain;
using ContactExtractor.Api.Domain.ValueObjects;
using ContactExtractor.Api.Services;

namespace ContactExtractor.Tests;

public class DuplicateDetectionTests
{
    private readonly DuplicateDetectionService _service = new();

    private static Contact MakeContact(
        Guid sessionId,
        string? email = null,
        string? phone = null,
        string? firstName = null,
        string? lastName = null,
        string? org = null)
    {
        var c = new Contact(sessionId)
        {
            FirstName    = firstName,
            LastName     = lastName,
            Organization = org
        };
        c.SetEmail(email is not null ? EmailAddress.TryCreate(email) : null);
        c.SetPhone(phone is not null ? PhoneNumber.TryCreate(phone) : null);
        return c;
    }

    [Fact]
    public void Score_ExactEmailMatch_ReturnsOne()
    {
        var a = MakeContact(Guid.Empty, email: "ola@example.no");
        var b = MakeContact(Guid.Empty, email: "ola@example.no");

        _service.Score(a, b).ShouldBe(1.0);
    }

    [Fact]
    public void Score_ExactPhoneMatch_Returns0Point9()
    {
        var a = MakeContact(Guid.Empty, phone: "99887766");
        var b = MakeContact(Guid.Empty, phone: "99887766");

        _service.Score(a, b).ShouldBe(0.9);
    }

    [Fact]
    public void Score_DifferentContacts_ReturnsLowScore()
    {
        var a = MakeContact(Guid.Empty, email: "alice@example.com", firstName: "Alice", lastName: "Smith");
        var b = MakeContact(Guid.Empty, email: "bob@example.com",   firstName: "Bob",   lastName: "Jones");

        _service.Score(a, b).ShouldBeLessThan(0.7);
    }

    [Fact]
    public void Score_FuzzyNameMatch_WithSameOrg_Returns0Point8()
    {
        var a = MakeContact(Guid.Empty, firstName: "Ola",  lastName: "Nordmann", org: "Eksempel AS");
        var b = MakeContact(Guid.Empty, firstName: "Ola",  lastName: "Nordmann", org: "Eksempel AS");

        _service.Score(a, b).ShouldBeGreaterThanOrEqualTo(0.8);
    }

    [Fact]
    public void FindDuplicates_GroupsExactEmailMatches()
    {
        var id = Guid.Empty;
        var contacts = new List<Contact>
        {
            MakeContact(id, email: "ola@example.no", firstName: "Ola",  lastName: "Nordmann"),
            MakeContact(id, email: "ola@example.no", firstName: "Ola",  lastName: "Nordman"),   // typo
            MakeContact(id, email: "kari@example.no", firstName: "Kari", lastName: "Nordmann")
        };

        var groups = _service.FindDuplicates(contacts);

        groups.Count.ShouldBe(1);
        groups[0].Indices.Count.ShouldBe(2);
    }

    [Fact]
    public void FindDuplicates_ReturnsEmpty_WhenNoContacts()
    {
        _service.FindDuplicates([]).ShouldBeEmpty();
    }

    [Fact]
    public void FindDuplicates_ReturnsEmpty_WhenAllUnique()
    {
        var id = Guid.Empty;
        var contacts = new List<Contact>
        {
            MakeContact(id, email: "alice@a.com", firstName: "Alice", lastName: "Anderson"),
            MakeContact(id, email: "bob@b.com",   firstName: "Bob",   lastName: "Brown"),
            MakeContact(id, email: "carol@c.com", firstName: "Carol", lastName: "Clark")
        };

        _service.FindDuplicates(contacts).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("ola nordmann", "ola nordmann", 1.0)]
    [InlineData("ola nordmann", "ole nordmann", true)]   // similar
    [InlineData("alice",        "bob",          false)]  // very different
    public void JaroWinkler_ReturnsExpectedSimilarity(string s1, string s2, object expected)
    {
        var score = DuplicateDetectionService.JaroWinkler(s1, s2);
        if (expected is double d)
            score.ShouldBe(d, 0.01);
        else if ((bool)expected)
            score.ShouldBeGreaterThan(0.7);
        else
            score.ShouldBeLessThan(0.5);
    }
}
