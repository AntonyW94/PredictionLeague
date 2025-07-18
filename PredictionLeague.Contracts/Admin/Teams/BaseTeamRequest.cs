namespace PredictionLeague.Contracts.Admin.Teams;

public class BaseTeamRequest
{
    public string Name { get; set; } = string.Empty;
    public string LogoUrl { get; set; } = string.Empty;
}