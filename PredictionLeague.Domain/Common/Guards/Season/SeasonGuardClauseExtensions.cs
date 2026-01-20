using Ardalis.GuardClauses;

namespace PredictionLeague.Domain.Common.Guards.Season;

public static class SeasonGuardClauseExtensions
{
    public static void InvalidSeasonDuration(this IGuardClause _, DateTime startDateUtc, DateTime endDateUtc)
    {
        if (endDateUtc <= startDateUtc)
            throw new ArgumentException("End date must be after the start date.", nameof(endDateUtc));

        if (endDateUtc > startDateUtc.AddMonths(10))
            throw new ArgumentException("A season cannot span more than 10 months.");
    }
}
