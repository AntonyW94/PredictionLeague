using Ardalis.GuardClauses;

namespace PredictionLeague.Domain.Common.Guards.Season
{
    public static class GuardClauseExtensions
    {
        public static void InvalidSeasonDuration(this IGuardClause _, DateTime startDate, DateTime endDate)
        {
            if (endDate <= startDate)
                throw new ArgumentException("End date must be after the start date.", nameof(endDate));
            
            if (endDate > startDate.AddMonths(10))
                throw new ArgumentException("A season cannot span more than 10 months.");
        }
    }
}
