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
        await _leagueRepository.AddMemberAsync(league.Id, administratorUserId);

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

        var members = await _leagueRepository.GetMembersByLeagueIdAsync(league.Id);
        if (members.Any(m => m.UserId == userId))
            return;

        await _leagueRepository.AddMemberAsync(league.Id, userId);
    }

    public async Task JoinPublicLeagueAsync(int leagueId, string userId)
    {
        // In a real app, you would add more validation here.
        await _leagueRepository.AddMemberAsync(leagueId, userId);
    }

    public async Task<League?> GetDefaultPublicLeagueAsync()
    {
        return await _leagueRepository.GetDefaultPublicLeagueAsync();
    }
}