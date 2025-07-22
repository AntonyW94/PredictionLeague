using Ardalis.GuardClauses;
using PredictionLeague.Domain.Common.Enumerations;

namespace PredictionLeague.Domain.Models;

public class Match
{
    public int Id { get; init; }
    public int RoundId { get; private set; }
    public int HomeTeamId { get; private set; }
    public int AwayTeamId { get; private set; }
    public DateTime MatchDateTime { get; private set; }
    public MatchStatus Status { get; private set; }
    public int? ActualHomeTeamScore { get; private set; }
    public int? ActualAwayTeamScore { get; private set; }

    public Team? HomeTeam { get; set; }
    public Team? AwayTeam { get; set; }

    private Match() { }

    public static Match Create(int roundId, int homeTeamId, int awayTeamId, DateTime matchDateTime)
    {
        Guard.Against.Default(matchDateTime, nameof(matchDateTime));
        Guard.Against.Expression(h => h == awayTeamId, homeTeamId, "A team cannot play against itself.");

        return new Match
        {
            RoundId = roundId,
            HomeTeamId = homeTeamId,
            AwayTeamId = awayTeamId,
            MatchDateTime = matchDateTime,
            Status = MatchStatus.Scheduled
        };
    }

    public void UpdateScore(int homeScore, int awayScore, MatchStatus status)
    {
        Guard.Against.Negative(homeScore, nameof(homeScore));
        Guard.Against.Negative(awayScore, nameof(awayScore));

        ActualHomeTeamScore = homeScore;
        ActualAwayTeamScore = awayScore;
        Status = status;
    }
}