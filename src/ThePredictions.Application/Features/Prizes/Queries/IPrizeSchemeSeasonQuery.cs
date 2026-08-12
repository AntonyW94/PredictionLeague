namespace ThePredictions.Application.Features.Prizes.Queries;

/// <summary>Reads the season a prize scheme is being evaluated against, or nothing if there is no such season.</summary>
public interface IPrizeSchemeSeasonQuery
{
    Task<PrizeSchemeSeasonRow?> ExecuteAsync(int seasonId, CancellationToken cancellationToken);
}
