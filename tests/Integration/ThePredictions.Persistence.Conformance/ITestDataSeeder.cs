using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Persistence.Conformance;

/// <summary>
/// Everything a conformance test needs to put into the database, expressed without a dialect.
///
/// Arrangement deliberately bypasses the repositories: a test that arranges through the write path it is
/// about to assert on can be fooled by that write path. Each adapter therefore implements this with its
/// own direct statements, which is also the only honest option - the schema belongs to the adapter, so
/// there is no dialect-free way to insert a row.
///
/// The surface is complete rather than minimal: it covers every table the conformance suite will need as
/// phase 2 moves query tests across, not only what today's tests touch. That is the point of an interface
/// here - a second adapter learns its full obligation from the compiler, on the day it is created, rather
/// than discovering it one failing test at a time.
///
/// Every method returns the identity the database generated. Nothing may assume an id, because a reset
/// between tests is not required to reseed identity columns.
/// </summary>
public interface ITestDataSeeder
{
    /// <summary>A competition, an active season, two teams to play each other, and one player.</summary>
    Task<SeededBackdrop> AddBackdropAsync();

    Task<int> AddCompetitionAsync(string code = "TEST");

    Task<int> AddSeasonAsync(int competitionId, string name = "2026/27", int numberOfRounds = 38);

    Task<int> AddTeamAsync(string name, string abbreviation);

    Task<string> AddUserAsync(string firstName, string lastName);

    Task<int> AddRoundAsync(
        int seasonId,
        int roundNumber,
        DateTime deadlineUtc,
        RoundStatus status = RoundStatus.Published,
        DateTime? startDateUtc = null);

    Task<int> AddMatchAsync(
        int roundId,
        int? homeTeamId,
        int? awayTeamId,
        DateTime? matchDateTimeUtc = null,
        DateTime? customLockTimeUtc = null,
        MatchStatus status = MatchStatus.Scheduled,
        int? matchNumber = null);

    Task AddPredictionAsync(int matchId, string userId, int homeScore = 2, int awayScore = 1);

    Task<int> AddLeagueAsync(int seasonId, string administratorUserId, string name = "Integration League");

    Task AddLeagueMemberAsync(int leagueId, string userId, LeagueMemberStatus status = LeagueMemberStatus.Approved);

    Task<int> AddBoostDefinitionAsync(string code, string name, string scope = "Round");

    Task<int> AddLeagueBoostRuleAsync(int leagueId, int boostDefinitionId, int totalUsesPerSeason = 2, bool isEnabled = true);

    Task AddBoostUsageAsync(string userId, int leagueId, int seasonId, int roundId, int boostDefinitionId);

    Task AddLeagueRoundResultAsync(int leagueId, int roundId, string userId, int basePoints, int boostedPoints, string appliedBoostCode);

    /// <summary>
    /// A global per-user-per-round outcome tally. Distinct from <see cref="AddLeagueRoundResultAsync"/>: that one
    /// is per league and carries points, this one is league-agnostic and carries outcome counts.
    /// </summary>
    Task AddRoundResultAsync(int roundId, string userId, int exactScoreCount, int correctResultCount = 0, int incorrectCount = 0);

    /// <summary>
    /// Deletes a match with no guard at all. Not seeding, but the same category: a direct statement that
    /// bypasses the code under test. It exists so a conformance test can demonstrate that the adapter's
    /// schema cascades the delete to predictions, which is the reason the repository needs its guard.
    /// </summary>
    Task DeleteMatchAsync(int matchId);
}
