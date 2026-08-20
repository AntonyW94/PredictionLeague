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

    /// <summary>
    /// A season. <paramref name="isActive"/> is what several reads scope on, so a test has to be able to arrange a season that
    /// has been retired.
    /// </summary>
    Task<int> AddSeasonAsync(int competitionId, string name = "2026/27", int numberOfRounds = 38, bool isActive = true);

    Task<int> AddTeamAsync(string name, string abbreviation);

    /// <summary>
    /// A user. <paramref name="email"/> defaults to one derived from the name, which is all a query test
    /// needs.
    /// </summary>
    /// <param name="password">
    /// Supply one only when the user has to <b>sign in</b>, which in practice means a browser journey. It
    /// writes a real hash and a security stamp, both of which ASP.NET Identity requires to accept a
    /// sign-in; left null - as every query test leaves it - the row cannot authenticate, which is correct
    /// for a test that only reads.
    /// </param>
    /// <param name="phoneNumber">
    /// A mobile number, which the onboarding checklist reads as one of its four steps. Left null - as most tests leave it -
    /// the step is outstanding.
    /// </param>
    /// <param name="termsAcceptedAtUtc">
    /// When they accepted the terms. Null is a real state, not just an unset default: accounts that predate the click-wrap
    /// wording on Register have no stored proof of consent, and the administrator's list flags them.
    /// </param>
    /// <param name="marketingOptInAtUtc">When they opted in to marketing email, or null if they never did.</param>
    /// <param name="createdAtUtc">
    /// When the account was registered. Null is a real state here too: accounts that predate migration 0011 were
    /// backfilled from their earliest provable activity, and the few with no activity at all kept null - which the
    /// administrator's list shows as an unknown join date rather than inventing one.
    /// </param>
    Task<string> AddUserAsync(
        string firstName,
        string lastName,
        string? email = null,
        string? password = null,
        string? phoneNumber = null,
        DateTime? termsAcceptedAtUtc = null,
        DateTime? marketingOptInAtUtc = null,
        DateTime? createdAtUtc = null);

    /// <summary>
    /// A round. <paramref name="completedDateUtc"/> is separate from <paramref name="status"/> on purpose: a round
    /// marked complete with no completion date is a state the database allows and the active-round rule has to cope
    /// with, so a test has to be able to arrange it.
    /// </summary>
    /// <remarks>
    /// <paramref name="displayName"/> is what the round is called - "Gameweek 12" for a league round, "Quarter Finals" for
    /// a tournament stage. The column does not allow null, so the default is a name rather than nothing; pass an empty
    /// string to arrange the blank a read has to cope with, because naming such a round by its number is a rule.
    /// </remarks>
    /// <summary>
    /// Puts an existing user in an existing Identity role.
    /// </summary>
    /// <remarks>
    /// The role has to be there already, and in a browser suite that is a matter of timing rather than of
    /// seeding: <c>DatabaseInitialiser</c> writes the roles from the <c>ApplicationUserRole</c> enum when the
    /// application starts, so this can only be called <b>after</b> the application is up. Calling it before
    /// throws rather than quietly leaving the user unprivileged, because an admin journey that silently ran as
    /// a player would fail somewhere unrelated - on a 403, or on a page that simply did not render.
    /// </remarks>
    Task AddUserToRoleAsync(string userId, string roleName);

    Task<int> AddRoundAsync(
        int seasonId,
        int roundNumber,
        DateTime deadlineUtc,
        RoundStatus status = RoundStatus.Published,
        DateTime? startDateUtc = null,
        DateTime? completedDateUtc = null,
        string? displayName = null);

    Task<int> AddMatchAsync(
        int roundId,
        int? homeTeamId,
        int? awayTeamId,
        DateTime? matchDateTimeUtc = null,
        DateTime? customLockTimeUtc = null,
        MatchStatus status = MatchStatus.Scheduled,
        int? matchNumber = null);

    Task AddPredictionAsync(
        int matchId,
        string userId,
        int homeScore = 2,
        int awayScore = 1,
        PredictionOutcome outcome = PredictionOutcome.Pending);

    /// <summary>
    /// A league. <paramref name="hasPrizes"/> is what several reads scope on - a league that pays nothing will never send anybody
    /// money - so a test has to be able to arrange one that does.
    /// </summary>
    /// <remarks>
    /// <paramref name="entryDeadlineUtc"/> is the moment entry closes, and null - no deadline set - is what the column defaults
    /// to. The welcome-email scan reads it as a window, so a test has to be able to place a league inside or outside one.
    /// <paramref name="entryCode"/> null makes the league public, which is how the join flow tells the two apart.
    /// </remarks>
    Task<int> AddLeagueAsync(
        int seasonId,
        string administratorUserId,
        string name = "Integration League",
        bool hasPrizes = false,
        DateTime? entryDeadlineUtc = null,
        string? entryCode = null,
        decimal price = 0m,
        decimal? prizeFundOverride = null);

    Task AddLeagueMemberAsync(int leagueId, string userId, LeagueMemberStatus status = LeagueMemberStatus.Approved);

    Task<int> AddBoostDefinitionAsync(string code, string name, string scope = "Round");

    Task<int> AddLeagueBoostRuleAsync(int leagueId, int boostDefinitionId, int totalUsesPerSeason = 2, bool isEnabled = true);

    /// <summary>
    /// A stretch of rounds in which one of a league's boosts may be used, and how often within it. A rule with no window at all
    /// runs all season, so a test has to be able to arrange both.
    /// </summary>
    Task<int> AddLeagueBoostWindowAsync(
        int leagueBoostRuleId,
        int startRoundNumber,
        int endRoundNumber,
        int maxUsesInWindow);

    Task AddBoostUsageAsync(string userId, int leagueId, int seasonId, int roundId, int boostDefinitionId);

    Task AddLeagueRoundResultAsync(int leagueId, int roundId, string userId, int basePoints, int boostedPoints, string appliedBoostCode);

    /// <summary>
    /// Maps a round number in a season to a tournament stage. The <paramref name="stages"/> text is what the
    /// group-or-knockout classification reads; a round with no mapping at all classifies as knockout.
    /// </summary>
    Task AddTournamentRoundMappingAsync(int seasonId, int roundNumber, string stages);

    /// <summary>
    /// A global per-user-per-round outcome tally. Distinct from <see cref="AddLeagueRoundResultAsync"/>: that one
    /// is per league and carries points, this one is league-agnostic and carries outcome counts.
    /// </summary>
    Task AddRoundResultAsync(int roundId, string userId, int exactScoreCount, int correctResultCount = 0, int incorrectCount = 0);

    /// <summary>
    /// A prize slot on a league - what it is for, and what it pays.
    /// </summary>
    /// <remarks>
    /// The <paramref name="prizeType"/> is stored the way the write path stores it, which for SQL Server means
    /// the enum's numeric value in a text column. A seeder that wrote the friendly name instead would make the
    /// conformance test agree with an adapter nothing in production could produce.
    /// </remarks>
    Task<int> AddLeaguePrizeSettingAsync(
        int leagueId,
        PrizeType prizeType,
        decimal prizeAmount,
        int rank = 1,
        string? prizeDescription = null);

    /// <summary>
    /// The prize scheme an administrator sets before entries close: the shape of the prizes, from which the concrete amounts are
    /// worked out later.
    /// </summary>
    /// <remarks>
    /// A league can have a scheme with no prize settings frozen from it yet, and that half-configured state is what holds back a
    /// welcome email - so it has to be arrangeable on its own.
    /// </remarks>
    Task<int> AddLeaguePrizeSchemeAsync(int leagueId, string setByUserId);

    /// <summary>
    /// One category of a scheme, and the whole pounds of each entry that fund it.
    /// </summary>
    /// <remarks>
    /// <paramref name="category"/> is stored the way the write path stores it, which for SQL Server means the enum's name in a
    /// text column - unlike <see cref="AddLeaguePrizeSettingAsync"/>, which stores the numeric value. The difference is real and
    /// reproduced deliberately.
    /// </remarks>
    Task<int> AddLeaguePrizeSchemeEntryAsync(
        int leaguePrizeSchemeId,
        PrizeType category,
        int perEntryPounds,
        string? rankTableJson = null);

    /// <summary>A prize actually paid out to a player.</summary>
    Task AddWinningAsync(
        string userId,
        int leaguePrizeSettingId,
        decimal amount,
        DateTime? awardedDateUtc = null,
        int? roundNumber = null,
        int? month = null);

    /// <summary>
    /// A record that one player has already been emailed about one specific prize.
    /// </summary>
    /// <remarks>
    /// <paramref name="roundNumber"/> and <paramref name="month"/> are the prize's scope, and both null - a season-long prize -
    /// is a state the log holds. Matching that against a winning is the rule the sent-log exists for, so a test has to be able to
    /// arrange every combination.
    /// </remarks>
    Task AddPrizeNotificationAsync(string userId, int leaguePrizeSettingId, int? roundNumber = null, int? month = null);

    /// <summary>A record that one player has already had a league's welcome email.</summary>
    Task AddLeagueWelcomeNotificationAsync(int leagueId, string userId);

    /// <summary>
    /// A row in the cached ranking table the My Leagues tile reads. Every rank defaults to null, which is what the
    /// tile treats as "no such position" - so a test states only the ranks it is about.
    /// </summary>
    Task AddLeagueMemberStatsAsync(
        int leagueId,
        string userId,
        int? overallRank = null,
        int? monthRank = null,
        int? liveRoundRank = null,
        int? snapshotOverallRank = null,
        int? snapshotMonthRank = null,
        int? stableRoundRank = null,
        int? stageRank = null,
        int? preRoundStageRank = null,
        int? exactScoresRank = null,
        int? preRoundExactScoresRank = null);

    /// <summary>
    /// The single-row master email switch. No row is seeded in production, and its absence is meaningful - so a test
    /// has to be able to arrange both "absent" and "present and off", which look the same to a careless adapter.
    /// </summary>
    Task<int> AddEmailSettingsAsync(bool emailsEnabled);

    /// <summary>
    /// A recorded payout to one winner of a league. <paramref name="paidAtUtc"/> null means recorded but not yet paid,
    /// which the payouts screen has to tell apart from paid.
    /// </summary>
    Task AddLeaguePayoutAsync(int leagueId, string userId, decimal totalAmount, DateTime? paidAtUtc);

    /// <summary>
    /// A player's bank details for receiving prize money. The values stand in for ciphertext - the seeder writes them as
    /// given, because what matters to a conformance test is which rows come back rather than what they decrypt to.
    /// </summary>
    Task AddUserPayoutDetailsAsync(string userId, string? accountName, string? sortCode, string? accountNumber);

    /// <summary>
    /// A player's participation in a season. Every kind of participation has a row - purchased, trial or free - so this is
    /// what the "have they bought in yet" checks read.
    /// </summary>
    /// <remarks>
    /// The amounts default to zero, which is right for a trial or a free-season pass and is what most tests want. State
    /// them for a purchase: what an account has spent on passes counts <b>purchased</b> rows only, so a test about spend
    /// has to be able to arrange a paid pass and a comped one separately.
    /// </remarks>
    Task<int> AddSeasonPassAsync(
        string userId,
        int seasonId,
        SeasonPassTier tier = SeasonPassTier.Standard,
        SeasonPassSource source = SeasonPassSource.Purchased,
        decimal amountPaid = 0m,
        decimal smsFeePaid = 0m,
        DateTime? createdAtUtc = null);

    /// <summary>
    /// A record that one account dismissed one onboarding step.
    /// </summary>
    /// <remarks>
    /// Dismissing a step is not finishing it, and it does not stop it being finished later - so a test has to be able to
    /// arrange a skip both with and without the underlying data being there, because the two produce different states.
    /// </remarks>
    Task AddOnboardingSkipAsync(string userId, string stepKey, DateTime? skippedAtUtc = null);

    /// <summary>
    /// A badge a player has earned, and when. One row per award: a repeatable badge earned three times is three rows,
    /// which is what the badges page counts and what the leaderboard has to collapse to one.
    /// </summary>
    /// <remarks>
    /// <paramref name="roundId"/> is what makes a second award of the same badge possible. The write path's idempotency
    /// key is the badge plus the round and season it was scoped to, so two awards of one badge with no scope at all are
    /// the same award - a test that wants a badge won twice has to say which rounds it was won in.
    /// </remarks>
    /// <param name="seasonId">
    /// The season the badge was scoped to, or null for a lifetime badge. Separate from <paramref name="roundId"/>: the round
    /// exists to make a second award possible, whereas the season is what a reader shows the badge under.
    /// </param>
    /// <param name="detail">The caption extra the badge stores, such as the score or the streak length.</param>
    Task AddUserBadgeAsync(
        string userId,
        string badgeKey,
        DateTime awardedUtc,
        int? roundId = null,
        int? seasonId = null,
        string? detail = null);

    /// <summary>The pricing calculator's settings. A single-row table by convention, not by constraint.</summary>
    Task<int> AddPricingSettingsAsync(decimal bufferRate, decimal minimumFloor);

    /// <summary>One recurring cost of running the site. A null end date means it is still being paid.</summary>
    Task<int> AddRunningCostAsync(
        string name,
        decimal amount,
        string frequency,
        DateTime startDateUtc,
        DateTime? endDateUtc,
        string? notes);

    /// <summary>What one payment or messaging provider charges.</summary>
    Task<int> AddServiceFeeAsync(string provider, decimal percentFee, decimal fixedFee);

    /// <summary>
    /// Deletes a match with no guard at all. Not seeding, but the same category: a direct statement that
    /// bypasses the code under test. It exists so a conformance test can demonstrate that the adapter's
    /// schema cascades the delete to predictions, which is the reason the repository needs its guard.
    /// </summary>
    Task DeleteMatchAsync(int matchId);
}
