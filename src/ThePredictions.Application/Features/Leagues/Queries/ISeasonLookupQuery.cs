namespace ThePredictions.Application.Features.Leagues.Queries;

/// <summary>
/// Reads every season, for the pickers that offer one.
/// </summary>
/// <remarks>
/// Every season, not only the active ones: that a new league may only be created in an active season is a rule, and it is
/// applied by the handler.
/// </remarks>
public interface ISeasonLookupQuery
{
    Task<IReadOnlyList<SeasonLookupRow>> ExecuteAsync(CancellationToken cancellationToken);
}
