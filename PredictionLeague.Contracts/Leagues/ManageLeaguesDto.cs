namespace PredictionLeague.Contracts.Leagues;

public class ManageLeaguesDto
{
    public List<LeagueDto> PublicLeagues { get; set; } = new();
    public List<LeagueDto> MyPrivateLeagues { get; set; } = new();
    public List<LeagueDto> OtherPrivateLeagues { get; set; } = new();
}