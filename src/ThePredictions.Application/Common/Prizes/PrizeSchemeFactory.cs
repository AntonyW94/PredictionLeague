using ThePredictions.Contracts.Prizes;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Models;

namespace ThePredictions.Application.Common.Prizes;

/// <summary>
/// Builds a domain <see cref="LeaguePrizeScheme"/> from a <see cref="PrizeSchemeRequest"/>. The
/// domain factory enforces the invariants (allocations sum to the stake, category gating,
/// non-negative values); this just maps the request shape.
/// </summary>
public static class PrizeSchemeFactory
{
    public static LeaguePrizeScheme Build(PrizeSchemeRequest request, int stakePounds, string setByUserId, bool isTournament, IDateTimeProvider dateTimeProvider)
    {
        var entries = request.Categories
            .Select(c => LeaguePrizeSchemeEntry.Create(c.Category, c.PerEntryPounds, ValidatedJson(c.RankTableJson)))
            .ToList();

        return LeaguePrizeScheme.Create(
            stakePounds,
            entries,
            setByUserId,
            isTournament,
            dateTimeProvider);
    }

    private static string? ValidatedJson(string? rankTableJson)
    {
        if (string.IsNullOrWhiteSpace(rankTableJson))
            return null;

        // Parse-and-discard so an invalid override is rejected at set-time (throws ArgumentException).
        RankTableSerializer.Deserialize(rankTableJson);
        return rankTableJson;
    }
}
