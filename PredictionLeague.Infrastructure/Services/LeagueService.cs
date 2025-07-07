using PredictionLeague.Application.Repositories;
using PredictionLeague.Application.Services;
using PredictionLeague.Domain.Models;
using PredictionLeague.Shared.Leagues;

namespace PredictionLeague.Infrastructure.Services;

public class LeagueService : ILeagueService
{
    private readonly ILeagueRepository _leagueRepository;

    public LeagueService(ILeagueRepository leagueRepository)
    {
        _leagueRepository = leagueRepository;
    }

    public async Task<League> CreateLeagueAsync(CreateLeagueRequest request, string administratorUserId)
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

    public async Task<IEnumerable<League>> GetLeaguesForUserAsync(string userId)
    {
        return await _leagueRepository.GetLeaguesByUserIdAsync(userId);
    }

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
        // In a real app, you would add more logic here, e.g.,
        // - Check if the current user is the league administrator.
        // - Update the member's status from "Pending" to "Approved".
        // This is a placeholder for now.
        await Task.CompletedTask;
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

    public async Task<League?> GetDefaultPublicLeagueAsync()
    {
        return await _leagueRepository.GetDefaultPublicLeagueAsync();
    }
}