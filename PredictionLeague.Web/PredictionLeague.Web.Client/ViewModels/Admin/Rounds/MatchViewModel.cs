using PredictionLeague.Contracts.Admin.Rounds;
using PredictionLeague.Domain.Common.Enumerations;

namespace PredictionLeague.Web.Client.ViewModels.Admin.Rounds;

public class MatchViewModel
{
    public int MatchId { get; }
    public DateTime MatchDateTime { get; }
    public string HomeTeamName { get; }
    public string? HomeTeamLogoUrl { get; }
    public string AwayTeamName { get; }
    public string? AwayTeamLogoUrl { get; }
    public int HomeScore { get; private set; }
    public int AwayScore { get; private set; }
    public MatchStatus Status { get; set; }

    public MatchViewModel(MatchInRoundDto match)
    {
        MatchId = match.Id;
        MatchDateTime = match.MatchDateTime;
        HomeTeamName = match.HomeTeamName;
        HomeTeamLogoUrl = match.HomeTeamLogoUrl;
        AwayTeamName = match.AwayTeamName;
        AwayTeamLogoUrl = match.AwayTeamLogoUrl;
        HomeScore = match.ActualHomeScore ?? 0;
        AwayScore = match.ActualAwayScore ?? 0;
        Status = match.Status;
    }

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