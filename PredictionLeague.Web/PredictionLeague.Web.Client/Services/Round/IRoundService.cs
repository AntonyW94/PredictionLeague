namespace PredictionLeague.Web.Client.Services.Round;

public interface IRoundService
{
    Task<bool> SendChaseEmailsAsync(int roundId);
}