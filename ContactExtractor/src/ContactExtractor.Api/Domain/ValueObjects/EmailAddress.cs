namespace ContactExtractor.Api.Domain.ValueObjects;

public record EmailAddress
{
    public string Value { get; }

    public EmailAddress(string value)
    {
        if (!IsValid(value))
            throw new ArgumentException($"Ugyldig e-postadresse: {value}");
        Value = value.ToLowerInvariant().Trim();
    }

    // EF Core conversion constructor
    private EmailAddress() => Value = string.Empty;

    public static bool IsValid(string? email) =>
        !string.IsNullOrWhiteSpace(email) &&
        System.Text.RegularExpressions.Regex.IsMatch(email.Trim(),
            """^[^@\s]+@[^@\s]+\.[^@\s]+$""");

    public static EmailAddress? TryCreate(string? value) =>
        IsValid(value) ? new EmailAddress(value!) : null;

    public override string ToString() => Value;
}
