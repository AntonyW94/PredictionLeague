using Microsoft.AspNetCore.Identity;
using ThePredictions.Domain.Common;

namespace ThePredictions.Infrastructure.Identity;

/// <summary>
/// Identity lookup normaliser that canonicalises emails (lowercase, strip <c>+alias</c>) before
/// upper-casing for storage in NormalizedEmail/NormalizedUserName. This makes plus-aliases collide on
/// the unique email index, blocking repeat free-trial farming (ADR 0009). Usernames are emails here,
/// so both are normalised the same way.
/// </summary>
public class CanonicalEmailLookupNormalizer : ILookupNormalizer
{
    public string? NormalizeName(string? name) => Canonicalise(name);

    public string? NormalizeEmail(string? email) => Canonicalise(email);

    private static string? Canonicalise(string? value)
    {
        if (value is null)
            return null;

        return EmailNormaliser.ToCanonical(value).ToUpperInvariant();
    }
}
