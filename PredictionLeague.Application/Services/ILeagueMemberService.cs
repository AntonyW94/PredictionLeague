using PredictionLeague.Shared.Admin.Leagues;

namespace PredictionLeague.Application.Services;

public interface ILeagueMemberService
{
    Task<IEnumerable<LeagueMemberDto>> GetByLeagueIdAsync(int leagueId);
}