using System.Net;
using System.Text.RegularExpressions;

namespace ContactExtractor.Api.Services;

/// <summary>
/// Validerer e-postadresser (format + MX-record) og telefonnumre.
/// </summary>
public class ContactValidationService
{
    private static readonly Regex EmailRegex = new(
        """^[^@\s]+@[^@\s]+\.[^@\s]+$""", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex NorwegianPhoneRegex = new(
        """^(\+47)?[2-9]\d{7}$""", RegexOptions.Compiled);

    private static readonly Regex InternationalPhoneRegex = new(
        """^\+?[\d\s\-\(\)]{7,20}$""", RegexOptions.Compiled);

    /// <summary>
    /// Validerer e-postformat og forsøker DNS MX-oppslag for domenet.
    /// Returnerer false ved nettverksfeil (beste innsats).
    /// </summary>
    public async Task<bool> ValidateEmailAsync(string? email, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        if (!EmailRegex.IsMatch(email)) return false;

        // DNS MX-record sjekk
        try
        {
            var domain = email.Split('@')[1];
            var mxRecords = await Dns.GetHostAddressesAsync(domain, ct);
            return mxRecords.Length > 0;
        }
        catch
        {
            // Nettverksfeil eller ukjent domene – godta formatet, men merk som tvilsom
            return EmailRegex.IsMatch(email);
        }
    }

    /// <summary>Validerer telefonnummer (norsk format prioriteres, fallback til internasjonalt).</summary>
    public bool ValidatePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return false;
        var cleaned = new string(phone.Where(c => char.IsDigit(c) || c is '+' or ' ' or '-' or '(' or ')').ToArray()).Trim();
        return NorwegianPhoneRegex.IsMatch(cleaned) || InternationalPhoneRegex.IsMatch(cleaned);
    }

    /// <summary>Kjøres batch på en liste kontakter, setter IsValidEmail og IsValidPhone.</summary>
    public async Task ValidateBatchAsync(IEnumerable<Contact> contacts, CancellationToken ct = default)
    {
        foreach (var contact in contacts)
        {
            contact.IsValidEmail = await ValidateEmailAsync(contact.Email, ct);
            contact.IsValidPhone = ValidatePhone(contact.Phone);
        }
    }
}
