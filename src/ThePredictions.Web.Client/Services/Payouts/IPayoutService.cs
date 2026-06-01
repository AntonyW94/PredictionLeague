using ThePredictions.Contracts.Payouts;

namespace ThePredictions.Web.Client.Services.Payouts;

public interface IPayoutService
{
    Task<MyPayoutDetailsDto?> GetMyPayoutDetailsAsync();
    Task<(bool Success, string? ErrorMessage)> SetMyPayoutDetailsAsync(SetPayoutDetailsRequest request);
    Task<bool> DeleteMyPayoutDetailsAsync();
    Task<LeaguePayoutsDto?> GetLeaguePayoutsAsync(int leagueId);
    Task<(bool Success, string? ErrorMessage)> MarkPayoutPaidAsync(int leagueId, string winnerUserId);
}
