using Ardalis.GuardClauses;
using PredictionLeague.Domain.Common.Enumerations;
using System.Diagnostics.CodeAnalysis;

namespace PredictionLeague.Domain.Models;

public class Match
{
    public int Id { get; private set; }
    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")] 
    public int RoundId { get; private set; }
    public int HomeTeamId { get; private set; }
    public int AwayTeamId { get; private set; }
    public DateTime MatchDateTime { get; private set; }
    public MatchStatus Status { get; private set; }
    public int? ActualHomeTeamScore { get; private set; }
    public int? ActualAwayTeamScore { get; private set; }
    public int? ExternalId { get; private set; }

    private Match() { }

    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    public Match(int id, int roundId, int homeTeamId, int awayTeamId, DateTime matchDateTime, MatchStatus status, int? actualHomeTeamScore, int? actualAwayTeamScore, int? externalId)
    {
        Id = id;
        RoundId = roundId;
        HomeTeamId = homeTeamId;
        AwayTeamId = awayTeamId;
        MatchDateTime = matchDateTime;
        Status = status;
        ActualHomeTeamScore = actualHomeTeamScore;
        ActualAwayTeamScore = actualAwayTeamScore;
        ExternalId = externalId;
    }
    
    public static Match Create(int roundId, int homeTeamId, int awayTeamId, DateTime matchDateTime, int? externalId)
    {
        Guard.Against.Default(matchDateTime, nameof(matchDateTime));
        Guard.Against.Expression(h => h == awayTeamId, homeTeamId, "A team cannot play against itself.");

        return new Match
        {
            RoundId = roundId,
            HomeTeamId = homeTeamId,
            AwayTeamId = awayTeamId,
            MatchDateTime = matchDateTime,
            Status = MatchStatus.Scheduled,
            ExternalId = externalId
        };
    }

    public void UpdateScore(int homeScore, int awayScore, MatchStatus status)
    {
        Guard.Against.Negative(homeScore, nameof(homeScore));
        Guard.Against.Negative(awayScore, nameof(awayScore));

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

    public void UpdateDetails(int homeTeamId, int awayTeamId, DateTime matchDateTime)
    {
        Guard.Against.Default(matchDateTime, nameof(matchDateTime));
        Guard.Against.Expression(h => h == awayTeamId, homeTeamId, "A team cannot play against itself.");

        HomeTeamId = homeTeamId;
        AwayTeamId = awayTeamId;
        MatchDateTime = matchDateTime;
    }

    public void UpdateDate(DateTime newDate)
    {
        MatchDateTime = newDate;
    }
}