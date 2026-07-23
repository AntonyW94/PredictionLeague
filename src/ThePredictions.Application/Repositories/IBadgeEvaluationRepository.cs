using ThePredictions.Application.Features.Badges.Evaluation;

namespace ThePredictions.Application.Repositories;

/// <summary>
/// Read queries that decide who qualifies for each badge. This is a command-side repository (uses the
/// write connection) so it sees freshly committed round results during evaluation, per the CQRS rules.
/// </summary>
public interface IBadgeEvaluationRepository
{
    Task<IReadOnlyList<RoundUserResult>> GetRoundResultsAsync(int roundId, CancellationToken cancellationToken);

    Task<IReadOnlyList<UserCount>> GetSeasonCumulativeExactsAsync(int seasonId, int roundNumber, CancellationToken cancellationToken);

    Task<IReadOnlyList<UserCount>> GetStreaksEndingAtRoundAsync(int seasonId, int roundNumber, CancellationToken cancellationToken);

    Task<IReadOnlyList<UserLeague>> GetRoundWinnersAsync(int roundId, CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> GetBeatTheCrowdUsersAsync(int roundId, int minimumCrowd, CancellationToken cancellationToken);

    Task<IReadOnlyList<SocialiteAward>> GetSocialiteAwardsAsync(CancellationToken cancellationToken);

    /// <summary>Account/setup badges for all qualifying users (add mobile, add bank details, create a league).</summary>
    Task<IReadOnlyList<AccountBadgeAward>> GetAccountBadgeAwardsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<UserLeagueRank>> GetSeasonStandingsAsync(int seasonId, CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> GetEverPresentUsersAsync(int seasonId, CancellationToken cancellationToken);

    /// <summary>All completed round ids in chronological order (season, then round number) - drives the backfill.</summary>
    Task<IReadOnlyList<int>> GetCompletedRoundIdsAsync(CancellationToken cancellationToken);
}
