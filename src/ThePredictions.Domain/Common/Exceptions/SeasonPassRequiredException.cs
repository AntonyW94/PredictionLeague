namespace ThePredictions.Domain.Common.Exceptions;

public class SeasonPassRequiredException(int seasonId)
    : Exception($"A Season Pass is required to take part in season (ID: {seasonId}).")
{
    public int SeasonId { get; } = seasonId;
}
