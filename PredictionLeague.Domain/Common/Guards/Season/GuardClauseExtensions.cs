using Ardalis.GuardClauses;
using PredictionLeague.Domain.Common.Exceptions;
using System.Diagnostics.CodeAnalysis;

namespace PredictionLeague.Domain.Common.Guards.Season;

public static class GuardClauseExtensions
{
    public static void InvalidSeasonDuration(this IGuardClause _, DateTime startDateUtc, DateTime endDateUtc)
    {
        if (endDateUtc <= startDateUtc)
            throw new ArgumentException("End date must be after the start date.", nameof(endDateUtc));
            
        if (endDateUtc > startDateUtc.AddMonths(10))
            throw new ArgumentException("A season cannot span more than 10 months.");
    }

    public static void EntityNotFound<T>(this IGuardClause _, object key, [NotNull] T? input, string name = "Entity")
    {
        if (input is null)
            throw new EntityNotFoundException(name, key.ToString() ?? string.Empty);
    }
}