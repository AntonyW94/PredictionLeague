using Ardalis.GuardClauses;
using ThePredictions.Domain.Common.Enumerations;
using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Domain.Models;

public class Match
{
    public int Id { get; private set; }
    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")] 
    public int RoundId { get; private set; }
    public int? HomeTeamId { get; private set; }
    public int? AwayTeamId { get; private set; }
    public DateTime MatchDateTimeUtc { get; private set; }
    public DateTime? CustomLockTimeUtc { get; private set; }
    public MatchStatus Status { get; private set; }
    public int? ActualHomeTeamScore { get; private set; }
    public int? ActualAwayTeamScore { get; private set; }
    public int? ExternalId { get; private set; }
    public int? MatchNumber { get; private set; }
    public string? PlaceholderHomeName { get; private set; }
    public string? PlaceholderAwayName { get; private set; }
    public string? ApiRoundName { get; private set; }

    public bool AreTeamsConfirmed => HomeTeamId.HasValue && AwayTeamId.HasValue;

    private Match() { }

    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    public Match(int id, int roundId, int? homeTeamId, int? awayTeamId, DateTime matchDateTimeUtc, DateTime? customLockTimeUtc, MatchStatus status, int? actualHomeTeamScore, int? actualAwayTeamScore, int? externalId, int? matchNumber, string? placeholderHomeName, string? placeholderAwayName, string? apiRoundName)
    {
        Id = id;
        RoundId = roundId;
        HomeTeamId = homeTeamId;
        AwayTeamId = awayTeamId;
        MatchDateTimeUtc = matchDateTimeUtc;
        CustomLockTimeUtc = customLockTimeUtc;
        Status = status;
        ActualHomeTeamScore = actualHomeTeamScore;
        ActualAwayTeamScore = actualAwayTeamScore;
        ExternalId = externalId;
        MatchNumber = matchNumber;
        PlaceholderHomeName = placeholderHomeName;
        PlaceholderAwayName = placeholderAwayName;
        ApiRoundName = apiRoundName;
    }
    
    public static Match Create(int roundId, int homeTeamId, int awayTeamId, DateTime matchDateTimeUtc, int? externalId)
    {
        Guard.Against.NegativeOrZero(roundId, parameterName: null, message: "Round ID must be greater than 0");
        Guard.Against.Default(matchDateTimeUtc);
        Guard.Against.Expression(h => h == awayTeamId, homeTeamId, "A team cannot play against itself.");

        return new Match
        {
            RoundId = roundId,
            HomeTeamId = homeTeamId,
            AwayTeamId = awayTeamId,
            MatchDateTimeUtc = matchDateTimeUtc,
            CustomLockTimeUtc = null,
            Status = MatchStatus.Scheduled,
            ExternalId = externalId,
            PlaceholderHomeName = null,
            PlaceholderAwayName = null
        };
    }

    public static Match CreatePlaceholder(int roundId, string placeholderHomeName, string placeholderAwayName, string apiRoundName, int? matchNumber = null)
    {
        Guard.Against.NegativeOrZero(roundId, parameterName: null, message: "Round ID must be greater than 0");
        Guard.Against.NullOrWhiteSpace(placeholderHomeName, message: "Placeholder home name is required");
        Guard.Against.NullOrWhiteSpace(placeholderAwayName, message: "Placeholder away name is required");

        return new Match
        {
            RoundId = roundId,
            HomeTeamId = null,
            AwayTeamId = null,
            MatchDateTimeUtc = DateTime.MaxValue,
            CustomLockTimeUtc = null,
            Status = MatchStatus.Scheduled,
            ExternalId = null,
            MatchNumber = matchNumber,
            PlaceholderHomeName = placeholderHomeName,
            PlaceholderAwayName = placeholderAwayName,
            ApiRoundName = apiRoundName
        };
    }

    public void SetCustomLockTime(DateTime? customLockTimeUtc)
    {
        CustomLockTimeUtc = customLockTimeUtc;
    }

    public void SetExternalId(int externalId)
    {
        ExternalId = externalId;
    }

    public void SetApiRoundName(string apiRoundName)
    {
        ApiRoundName = apiRoundName;
    }

    public void UpdateScore(int homeScore, int awayScore, MatchStatus status)
    {
        Guard.Against.Negative(homeScore);
        Guard.Against.Negative(awayScore);

        if (status is MatchStatus.Scheduled or MatchStatus.Postponed)
        {
            ActualHomeTeamScore = null;
            ActualAwayTeamScore = null;
        }
        else
        {
            ActualHomeTeamScore = homeScore;
            ActualAwayTeamScore = awayScore;
        }
         
        Status = status;
    }

    public void UpdateDetails(int homeTeamId, int awayTeamId, DateTime matchDateTimeUtc)
    {
        Guard.Against.Default(matchDateTimeUtc);
        Guard.Against.Expression(h => h == awayTeamId, homeTeamId, "A team cannot play against itself.");

        HomeTeamId = homeTeamId;
        AwayTeamId = awayTeamId;
        MatchDateTimeUtc = matchDateTimeUtc;
    }

    public void UpdateDate(DateTime newDateUtc)
    {
        MatchDateTimeUtc = newDateUtc;
    }

    public void Postpone()
    {
        Status = MatchStatus.Postponed;
        ActualHomeTeamScore = null;
        ActualAwayTeamScore = null;
    }

    public void Reschedule()
    {
        if (Status == MatchStatus.Postponed)
            Status = MatchStatus.Scheduled;
    }

    public void MoveToRound(int newRoundId)
    {
        Guard.Against.NegativeOrZero(newRoundId);
        RoundId = newRoundId;
    }

    public DateTime GetEffectiveDeadline(DateTime roundDeadline)
    {
        return CustomLockTimeUtc ?? roundDeadline;
    }

    public bool IsPredictionLocked(DateTime utcNow, DateTime roundDeadline)
    {
        return utcNow >= GetEffectiveDeadline(roundDeadline);
    }

    /// <summary>
    /// Whether a player can still enter or change a prediction for this fixture: teams confirmed, still
    /// scheduled rather than kicked off, finished or postponed, and not yet locked.
    /// </summary>
    /// <remarks>
    /// This composition is the rule that two SQL predicates spent a year mirroring by comment - once in
    /// <c>GetRoundCompletionQueryHandler.PredictableMatchPredicate</c> and once inside
    /// <c>ReminderService.GetUsersMissingPredictionsAsync</c>, each carrying a note telling the reader to
    /// change both together. The pieces were already here (<see cref="AreTeamsConfirmed"/>,
    /// <see cref="IsPredictionLocked"/>); only the three-way composition was missing, so both call sites
    /// rewrote it in T-SQL instead. Getting it wrong means a player chased for predictions they cannot make,
    /// or not chased for ones they can.
    /// </remarks>
    public bool IsOpenForPrediction(DateTime utcNow, DateTime roundDeadline)
    {
        if (!AreTeamsConfirmed)
            return false;

        if (Status != MatchStatus.Scheduled)
            return false;

        return !IsPredictionLocked(utcNow, roundDeadline);
    }

    public void AssignTeams(int homeTeamId, int awayTeamId)
    {
        Guard.Against.Expression(h => h == awayTeamId, homeTeamId, "A team cannot play against itself.");

        HomeTeamId = homeTeamId;
        AwayTeamId = awayTeamId;
        PlaceholderHomeName = null;
        PlaceholderAwayName = null;
    }
}