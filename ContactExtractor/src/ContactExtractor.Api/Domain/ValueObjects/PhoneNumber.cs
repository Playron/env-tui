namespace ContactExtractor.Api.Domain.ValueObjects;

public record PhoneNumber
{
    public string Value { get; }
    public string? CountryCode { get; }

    public PhoneNumber(string value)
    {
        var cleaned = new string(value.Where(c => char.IsDigit(c) || c is '+' or ' ' or '-').ToArray());
        var digits = new string(cleaned.Where(c => char.IsDigit(c) || c is '+').ToArray());
        Value = digits;
        CountryCode = digits.StartsWith("+47") ? "+47"
                    : digits.StartsWith("+46") ? "+46"
                    : digits.StartsWith("+45") ? "+45"
                    : digits.StartsWith("+1")  ? "+1"
                    : null;
    }

    private PhoneNumber()
    {
        Value = string.Empty;
        CountryCode = null;
    }

    public static bool IsValid(string? phone) =>
        !string.IsNullOrWhiteSpace(phone) &&
        phone.Where(char.IsDigit).Count() >= 7;

    public static PhoneNumber? TryCreate(string? value) =>
        IsValid(value) ? new PhoneNumber(value!) : null;

    public override string ToString() => Value;
}
