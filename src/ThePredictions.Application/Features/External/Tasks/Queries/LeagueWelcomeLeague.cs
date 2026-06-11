namespace ThePredictions.Application.Features.External.Tasks.Queries;

/// <summary>
/// Everything needed to send one league's welcome emails: the league's headline facts, its frozen
/// prizes, its enabled boosts, and the approved members still to be welcomed.
/// </summary>
public record LeagueWelcomeLeague(
    int LeagueId,
    string LeagueName,
    string SeasonName,
    bool HasPrizes,
    int MemberCount,
    int NumberOfRounds,
    int NumberOfMonths,
    IReadOnlyList<LeagueWelcomePrize> Prizes,
    IReadOnlyList<LeagueWelcomeBoost> Boosts,
    IReadOnlyList<LeagueWelcomeRecipient> Recipients);
