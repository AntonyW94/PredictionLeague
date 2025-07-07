using PredictionLeague.Application.Repositories;
using PredictionLeague.Application.Services;
using PredictionLeague.Domain.Models;
using PredictionLeague.Shared.Admin.Leagues;
using PredictionLeague.Shared.Leagues;

namespace PredictionLeague.Infrastructure.Services;

public class LeagueService : ILeagueService
{
    private readonly ILeagueRepository _leagueRepository;
    private readonly ISeasonRepository _seasonRepository;

    public LeagueService(ILeagueRepository leagueRepository, ISeasonRepository seasonRepository)
    {
        _leagueRepository = leagueRepository;
        _seasonRepository = seasonRepository;
    }

    #region Create

    public async Task<League> CreateAsync(CreateLeagueRequest request, string administratorUserId)
    {
        var league = new League
        {
            Name = request.Name,
            SeasonId = request.SeasonId,
            AdministratorUserId = administratorUserId,
            EntryCode = string.IsNullOrWhiteSpace(request.EntryCode) ? null : request.EntryCode
        };

        await _leagueRepository.CreateAsync(league);

        var leagueMember = new LeagueMember
        {
            LeagueId = league.Id,
            UserId = administratorUserId,
            JoinedAt = DateTime.UtcNow,
            Status = "Approved"
        };

        await _leagueRepository.AddMemberAsync(leagueMember);

        return league;
    }

    #endregion

    #region Read

    public async Task<IEnumerable<LeagueDto>> GetAllAsync()
    {
        var leagues = await _leagueRepository.GetAllAsync();
        var leaguesToReturn = new List<LeagueDto>();

        foreach (var league in leagues)
        {
            var season = await _seasonRepository.GetByIdAsync(league.SeasonId);
            var members = await _leagueRepository.GetMembersByLeagueIdAsync(league.Id);

            leaguesToReturn.Add(new LeagueDto
            {
                Id = league.Id,
                Name = league.Name,
                SeasonName = season?.Name ?? "N/A",
                MemberCount = members.Count(),
                EntryCode = league.EntryCode ?? "Public"
            });
        }

        return leaguesToReturn;
    }

    public async Task<IEnumerable<League>> GetLeaguesForUserAsync(string userId)
    {
        return await _leagueRepository.GetLeaguesByUserIdAsync(userId);
    }

    public async Task<League?> GetDefaultPublicLeagueAsync()
    {
        return await _leagueRepository.GetDefaultPublicLeagueAsync();
    }

    #endregion

    #region Update

    public async Task UpdateAsync(int id, UpdateLeagueRequest request)
    {
        var league = await _leagueRepository.GetByIdAsync(id) ?? throw new KeyNotFoundException("League not found.");

        league.Name = request.Name;
        league.EntryCode = string.IsNullOrWhiteSpace(request.EntryCode) ? null : request.EntryCode;

        await _leagueRepository.UpdateAsync(league);
    }

    #endregion

    public async Task JoinLeagueAsync(string entryCode, string userId)
    {
        var leagues = await _leagueRepository.GetAllAsync();
        var league = leagues.FirstOrDefault(l => l.EntryCode == entryCode);

        if (league == null)
            throw new Exception("Invalid league entry code.");

        if (league.EntryDeadline.HasValue && league.EntryDeadline.Value < DateTime.UtcNow)
            throw new Exception("The deadline to join this league has passed.");

        var members = await _leagueRepository.GetMembersByLeagueIdAsync(league.Id);
        if (members.Any(m => m.UserId == userId))
            return;

        var newMember = new LeagueMember
        {
            LeagueId = league.Id,
            UserId = userId,
            Status = "Pending",
            JoinedAt = DateTime.UtcNow
        };

        await _leagueRepository.AddMemberAsync(newMember);
    }

    public async Task ApproveLeagueMemberAsync(int leagueId, string memberId)
    {
        await _leagueRepository.UpdateMemberStatusAsync(leagueId, memberId, "Approved");
    }

    public async Task JoinPublicLeagueAsync(int leagueId, string userId)
    {
        var newMember = new LeagueMember
        {
            LeagueId = leagueId,
            UserId = userId,
            Status = "Pending",
            JoinedAt = DateTime.UtcNow
        };

        await _leagueRepository.AddMemberAsync(newMember);
    }
}