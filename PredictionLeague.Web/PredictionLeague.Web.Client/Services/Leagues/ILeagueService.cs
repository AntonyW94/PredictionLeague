namespace PredictionLeague.Web.Client.Services.Leagues;

public interface ILeagueService
{
    Task<(bool Success, string? ErrorMessage)> JoinPublicLeagueAsync(int leagueId);
    Task<(bool Success, string? ErrorMessage)> JoinPrivateLeagueAsync(string entryCode);
}