using ThePredictions.Contracts.Admin.Seasons;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.Admin.Seasons.Queries;

/// <summary>
/// What the administrator's season screens work out from the rows: how the season's rounds are progressing, how many
/// teams are in it, and the order the list is shown in.
/// </summary>
internal static class AdminSeasons
{
    /// <summary>Newest first, which is where an administrator's attention is.</summary>
    public static IEnumerable<AdminSeasonRow> NewestFirst(IEnumerable<AdminSeasonRow> seasons) =>
        seasons.OrderByDescending(season => season.StartDateUtc);

    public static SeasonDto ToDto(AdminSeasonRow season, SeasonsData data)
    {
        var rounds = data.Rounds.Where(round => round.SeasonId == season.Id).ToList();

        return new SeasonDto(
            season.Id,
            season.Name,
            season.StartDateUtc,
            season.EndDateUtc,
            season.IsActive,
            season.NumberOfRounds,
            season.CompetitionId,
            season.CompetitionName,
            season.CompetitionType,
            season.ApiLeagueId,
            rounds.Count,
            rounds.Count(round => round.Status == RoundStatus.Draft),
            rounds.Count(round => round.Status == RoundStatus.Published),
            rounds.Count(round => round.Status == RoundStatus.InProgress),
            rounds.Count(round => round.Status == RoundStatus.Completed),
            TeamCount(season.Id, data),
            season.PassStandardPrice,
            season.PassPremiumPrice,
            season.PassHolderCount);
    }

    /// <summary>
    /// How many teams are in the season, counted from the fixtures of its <b>first</b> round.
    /// </summary>
    /// <remarks>
    /// The first round, not all of them - which is the right answer for a league, where every team plays in every round,
    /// and deliberately different from the season-teams read used by the season-pass page, which looks across the whole
    /// season. For a knockout competition the two disagree: the first round has the entrants, the whole season has the
    /// same teams again. Nothing records which teams are in a season, so both are inferences, and they are inferences
    /// about different questions. This was a nested <c>UNION</c> inside a correlated <c>COUNT</c>, where the difference
    /// was invisible.
    ///
    /// A fixture with no team yet contributes none, which is how a knockout round before its ties are settled counts
    /// nothing rather than counting placeholders.
    /// </remarks>
    private static int TeamCount(int seasonId, SeasonsData data)
    {
        var firstRoundNumber = data.Rounds
            .Where(round => round.SeasonId == seasonId)
            .Select(round => round.RoundNumber)
            .DefaultIfEmpty(0)
            .Min();

        return data.Fixtures
            .Where(fixture => fixture.SeasonId == seasonId && fixture.RoundNumber == firstRoundNumber)
            .SelectMany(fixture => new[] { fixture.HomeTeamId, fixture.AwayTeamId })
            .OfType<int>()
            .Distinct()
            .Count();
    }
}
