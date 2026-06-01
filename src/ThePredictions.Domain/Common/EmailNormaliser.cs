namespace ThePredictions.Domain.Common;

/// <summary>
/// Produces a canonical key for an email address so that plus-aliases map to the same account
/// (ADR 0009): trims, lowercases, and strips any <c>+suffix</c> from the local part. This stops
/// <c>you+tag@x.com</c> being used as a distinct account to farm repeated free trials. The original
/// entered address is still used for delivery; only the uniqueness/lookup key is canonicalised.
/// </summary>
public static class EmailNormaliser
{
    public static string ToCanonical(string? email)
    {
        var trimmed = (email ?? string.Empty).Trim().ToLowerInvariant();

        var atIndex = trimmed.IndexOf('@');
        if (atIndex <= 0)
            return trimmed;

        var local = trimmed[..atIndex];
        var domain = trimmed[atIndex..]; // includes '@'

        var plusIndex = local.IndexOf('+');
        if (plusIndex >= 0)
            local = local[..plusIndex];

        return local + domain;
    }
}
