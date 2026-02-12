namespace PredictionLeague.Contracts.Leagues;

public class ManageLeaguesDto
{
    public List<LeagueDto> PublicLeagues { get; set; } = [];
    public List<LeagueDto> MyPrivateLeagues { get; set; } = [];
    public List<LeagueDto> OtherPrivateLeagues { get; set; } = [];
}