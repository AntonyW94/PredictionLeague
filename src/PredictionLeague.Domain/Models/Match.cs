using Ardalis.GuardClauses;
using PredictionLeague.Domain.Common.Enumerations;
using System.Diagnostics.CodeAnalysis;

namespace PredictionLeague.Domain.Models;

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
    public string? PlaceholderHomeName { get; private set; }
    public string? PlaceholderAwayName { get; private set; }

    private Match() { }

    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    public Match(int id, int roundId, int homeTeamId, int awayTeamId, DateTime matchDateTimeUtc, DateTime? customLockTimeUtc, MatchStatus status, int? actualHomeTeamScore, int? actualAwayTeamScore, int? externalId, string? placeholderHomeName, string? placeholderAwayName)
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
        PlaceholderHomeName = placeholderHomeName;
        PlaceholderAwayName = placeholderAwayName;
    }
    
    public static Match Create(int roundId, int homeTeamId, int awayTeamId, DateTime matchDateTimeUtc, int? externalId)
    {
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

    public void UpdateScore(int homeScore, int awayScore, MatchStatus status)
    {
        Guard.Against.Negative(homeScore);
        Guard.Against.Negative(awayScore);

        if (status == MatchStatus.Scheduled)
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

    public void MoveToRound(int newRoundId)
    {
        Guard.Against.NegativeOrZero(newRoundId);
        RoundId = newRoundId;
    }
}