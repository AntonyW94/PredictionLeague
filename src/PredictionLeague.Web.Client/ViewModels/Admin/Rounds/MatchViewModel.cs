using PredictionLeague.Contracts.Admin.Rounds;
using PredictionLeague.Domain.Common.Enumerations;

namespace PredictionLeague.Web.Client.ViewModels.Admin.Rounds;

public class MatchViewModel(MatchInRoundDto match)
{
    public int MatchId { get; } = match.Id;
    public DateTime MatchDateTimeUtc { get; } = match.MatchDateTimeUtc;
    public string HomeTeamName { get; } = match.HomeTeamName;
    public string? HomeTeamLogoUrl { get; } = match.HomeTeamLogoUrl;
    public string AwayTeamName { get; } = match.AwayTeamName;
    public string? AwayTeamLogoUrl { get; } = match.AwayTeamLogoUrl;
    public int HomeScore { get; private set; } = match.ActualHomeTeamScore ?? 0;
    public int AwayScore { get; private set; } = match.ActualAwayTeamScore ?? 0;
    public MatchStatus Status { get; set; } = match.Status;

    public void UpdateScore(bool isHomeTeam, int delta)
    {
        if (isHomeTeam)
        {
            var newScore = HomeScore + delta;
            if (newScore >= 0 && newScore <= 9)
                HomeScore = newScore;
        }
        else
        {
            var newScore = AwayScore + delta;
            if (newScore >= 0 && newScore <= 9)
                AwayScore = newScore;
        }
    }
}