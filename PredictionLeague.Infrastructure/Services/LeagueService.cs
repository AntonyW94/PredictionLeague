using PredictionLeague.Core.Models;
using PredictionLeague.Core.Repositories;
using PredictionLeague.Core.Services;
using PredictionLeague.Shared.Dashboard;

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

    public async Task<League> CreateLeagueAsync(string name, int seasonId, string administratorUserId)
    {
        // Optional: Validate that seasonId is valid
        var season = await _seasonRepository.GetByIdAsync(seasonId);
        if (season == null)
            throw new Exception("Invalid Season specified.");

        var league = new League
        {
            Name = name,
            SeasonId = seasonId,
            AdministratorUserId = administratorUserId,
            EntryCode = GenerateEntryCode() // Private method to create a random code
        };

        await _leagueRepository.CreateAsync(league);

        // The creator should automatically be a member of the league.
        await _leagueRepository.AddMemberAsync(league.Id, administratorUserId);

        return league;
    }

    public async Task<IEnumerable<League>> GetLeaguesForUserAsync(string userId)
    {
        return await _leagueRepository.GetLeaguesByUserIdAsync(userId);
    }

    public async Task JoinLeagueAsync(string entryCode, string userId)
    {
        var league = await _leagueRepository.GetByEntryCodeAsync(entryCode);
        if (league == null)
        {
            throw new Exception("Invalid league entry code.");
        }

        var members = await _leagueRepository.GetMembersByLeagueIdAsync(league.Id);
        if (members.Any(m => m.UserId == userId))
        {
            // User is already a member, do nothing or throw an exception
            return;
        }

        await _leagueRepository.AddMemberAsync(league.Id, userId);
    }

    public async Task JoinPublicLeagueAsync(int leagueId, string userId)
    {
        // In a real app, you would add more validation (e.g., does the league exist and is it public?)
        await _leagueRepository.AddMemberAsync(leagueId, userId);
    }

    public async Task<League?> GetDefaultPublicLeagueAsync()
    {
        return await _leagueRepository.GetDefaultPublicLeagueAsync();
    }

    public async Task<IEnumerable<PublicLeagueDto>> GetAllPublicLeaguesForUserAsync(string userId)
    {
        return await _leagueRepository.GetAllPublicLeaguesForUserAsync(userId);
    }

    private string GenerateEntryCode()
    {
        var chars = "ABCDEFGHJKLMNPQRSTUVWXYZ123456789";
        var random = new Random();
        var result = new char[6];
        for (var i = 0; i < result.Length; i++)
        {
            result[i] = chars[random.Next(chars.Length)];
        }
        return new string(result);
    }
}