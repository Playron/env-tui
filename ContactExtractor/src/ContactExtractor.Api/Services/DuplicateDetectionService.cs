namespace ContactExtractor.Api.Services;

/// <summary>
/// Oppdager duplikat-kontakter basert på eksakt og fuzzy matching.
/// Scorer fra 0 (ingen likhet) til 1 (identisk).
/// </summary>
public class DuplicateDetectionService
{
    private const double ExactEmailScore       = 1.0;
    private const double ExactPhoneScore       = 0.9;
    private const double FuzzyNameAndOrgScore  = 0.8;
    private const double FuzzyNameAloneScore   = 0.6;
    private const double DuplicateThreshold    = 0.7;

    /// <summary>
    /// Finner grupper av like kontakter blant en liste.
    /// Returnerer liste av grupper (lister av duplikat-indekser).
    /// </summary>
    public List<(List<int> Indices, double Score)> FindDuplicates(IList<Contact> contacts)
    {
        var groups = new List<(List<int>, double)>();
        var grouped = new HashSet<int>();

        for (var i = 0; i < contacts.Count; i++)
        {
            if (grouped.Contains(i)) continue;

            var group = new List<int> { i };
            double maxScore = 0;

            for (var j = i + 1; j < contacts.Count; j++)
            {
                if (grouped.Contains(j)) continue;

                var score = Score(contacts[i], contacts[j]);
                if (score >= DuplicateThreshold)
                {
                    group.Add(j);
                    grouped.Add(j);
                    maxScore = Math.Max(maxScore, score);
                }
            }

            if (group.Count > 1)
            {
                grouped.Add(i);
                groups.Add((group, maxScore));
            }
        }

        return groups;
    }

    /// <summary>Beregner likhetsscore mellom to kontakter.</summary>
    public double Score(Contact a, Contact b)
    {
        // Eksakt e-post → definitiv duplikat
        if (!string.IsNullOrWhiteSpace(a.Email) &&
            !string.IsNullOrWhiteSpace(b.Email) &&
            string.Equals(a.Email, b.Email, StringComparison.OrdinalIgnoreCase))
            return ExactEmailScore;

        // Eksakt telefon → nesten definitiv
        if (!string.IsNullOrWhiteSpace(a.Phone) &&
            !string.IsNullOrWhiteSpace(b.Phone) &&
            NormalizePhone(a.Phone) == NormalizePhone(b.Phone))
            return ExactPhoneScore;

        // Fuzzy navnematch
        var nameA = GetComparableName(a);
        var nameB = GetComparableName(b);

        if (string.IsNullOrWhiteSpace(nameA) || string.IsNullOrWhiteSpace(nameB))
            return 0;

        var nameSimilarity = JaroWinkler(nameA, nameB);

        if (nameSimilarity < 0.85) return nameSimilarity * 0.4;

        // Høy navnelikhet – sjekk om samme org
        var sameOrg = !string.IsNullOrWhiteSpace(a.Organization) &&
                      !string.IsNullOrWhiteSpace(b.Organization) &&
                      JaroWinkler(
                          a.Organization.ToLowerInvariant(),
                          b.Organization.ToLowerInvariant()) > 0.85;

        return sameOrg ? FuzzyNameAndOrgScore : FuzzyNameAloneScore;
    }

    private static string GetComparableName(Contact c)
    {
        if (!string.IsNullOrWhiteSpace(c.FullName))
            return c.FullName.ToLowerInvariant().Trim();
        var parts = new[] { c.FirstName, c.LastName }
            .Where(p => !string.IsNullOrWhiteSpace(p));
        return string.Join(" ", parts).ToLowerInvariant().Trim();
    }

    private static string NormalizePhone(string phone) =>
        new string(phone.Where(char.IsDigit).ToArray());

    /// <summary>Jaro-Winkler-likhet mellom to strenger. Returner 0.0–1.0.</summary>
    public static double JaroWinkler(string s1, string s2)
    {
        if (s1 == s2) return 1.0;
        if (s1.Length == 0 || s2.Length == 0) return 0.0;

        var matchDist = Math.Max(s1.Length, s2.Length) / 2 - 1;
        if (matchDist < 0) matchDist = 0;

        var s1Matches = new bool[s1.Length];
        var s2Matches = new bool[s2.Length];

        var matches = 0;
        var transpositions = 0;

        for (var i = 0; i < s1.Length; i++)
        {
            var start = Math.Max(0, i - matchDist);
            var end   = Math.Min(i + matchDist + 1, s2.Length);

            for (var j = start; j < end; j++)
            {
                if (s2Matches[j] || s1[i] != s2[j]) continue;
                s1Matches[i] = true;
                s2Matches[j] = true;
                matches++;
                break;
            }
        }

        if (matches == 0) return 0.0;

        var k = 0;
        for (var i = 0; i < s1.Length; i++)
        {
            if (!s1Matches[i]) continue;
            while (!s2Matches[k]) k++;
            if (s1[i] != s2[k]) transpositions++;
            k++;
        }

        var jaro = (matches / (double)s1.Length +
                    matches / (double)s2.Length +
                    (matches - transpositions / 2.0) / matches) / 3.0;

        // Winkler bonus for felles prefiks (maks 4 tegn)
        var prefixLen = 0;
        for (var i = 0; i < Math.Min(4, Math.Min(s1.Length, s2.Length)); i++)
        {
            if (s1[i] == s2[i]) prefixLen++;
            else break;
        }

        return jaro + prefixLen * 0.1 * (1 - jaro);
    }
}
