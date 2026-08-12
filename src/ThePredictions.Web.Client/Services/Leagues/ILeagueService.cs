using ThePredictions.Contracts.Boosts;
using ThePredictions.Contracts.Dashboard;
using ThePredictions.Contracts.Leaderboards;
using ThePredictions.Contracts.Leagues;
using ThePredictions.Contracts.Prizes;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Web.Client.Services.Leagues;

public interface ILeagueService
{
    Task<List<MyLeagueDto>> GetMyLeaguesAsync();
    Task<List<AvailableLeagueDto>> GetAvailableLeaguesAsync();
    Task<List<LeagueLeaderboardDto>> GetLeaderboardsAsync();
    Task<List<ActiveRoundDto>> GetActiveRoundsAsync();
    Task<byte[]?> GetShareCardAsync(int roundId, string theme);
    Task<List<LeaderboardEntryDto>> GetOverallLeaderboardAsync(int leagueId);
    Task<List<LeaderboardEntryDto>> GetMonthlyLeaderboardAsync(int leagueId, int month);
    Task<ExactScoresLeaderboardDto> GetExactScoresLeaderboardAsync(int leagueId);
    Task<List<LeagueRequestDto>> GetPendingRequestsAsync();
    Task<List<MonthDto>> GetMonthsForLeagueAsync(int leagueId);
    Task<List<StageDto>> GetStagesForLeagueAsync(int leagueId);
    Task<List<LeaderboardEntryDto>> GetStageLeaderboardAsync(int leagueId, TournamentStageGroup stage);
    Task<WinningsDto> GetWinningsAsync(int leagueId);
    Task<PrizeBreakdownDto?> GetPrizeBreakdownAsync(int leagueId);
    Task<LeaguePaymentInfoDto?> GetPaymentInfoAsync(int leagueId);
    Task<List<BoostUsageSummaryDto>> GetBoostUsageSummaryAsync(int leagueId);
    Task<bool> CheckForAvailablePrivateLeaguesAsync();
    Task<PendingMembersResultDto> GetPendingMembersForAdminAsync();
    Task UpdateMemberStatusAsync(int leagueId, string userId, LeagueMemberStatus newStatus);

    Task<(bool Success, string? ErrorMessage, bool NeedsSeasonPass)> JoinPublicLeagueAsync(int leagueId);
    Task<(PrizePreviewDto? Preview, string? ErrorMessage)> GetJoinPreviewByIdAsync(int leagueId);
    Task<(PrizePreviewDto? Preview, string? ErrorMessage)> GetJoinPreviewAsync(string entryCode);
    Task<(bool Success, string? ErrorMessage, int? LeagueId, bool NeedsSeasonPass)> JoinPrivateLeagueAsync(string entryCode);
    Task<(bool Success, string? ErrorMessage)> CancelJoinRequestAsync(int leagueId);
    Task<(bool Success, string? ErrorMessage)> DismissAlertAsync(int leagueId);
    Task<(bool Success, string? ErrorMessage)> SetLeagueArchivedAsync(int leagueId, bool isArchived);
}