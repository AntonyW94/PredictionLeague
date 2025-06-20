using PredictionLeague.Core.Models;
using PredictionLeague.Core.Repositories;
using PredictionLeague.Core.Services;

namespace PredictionLeague.Infrastructure.Services;

public class LeagueService : ILeagueService
{
    private readonly ILeagueRepository _leagueRepository;
    private readonly IGameYearRepository _gameYearRepository;

    public LeagueService(ILeagueRepository leagueRepository, IGameYearRepository gameYearRepository)
    {
        _leagueRepository = leagueRepository;
        _gameYearRepository = gameYearRepository;
    }

    public async Task<League> CreateLeagueAsync(string name, int gameYearId, string administratorUserId)
    {
        // Optional: Validate that gameYearId is valid
        var gameYear = await _gameYearRepository.GetByIdAsync(gameYearId);
        if (gameYear == null)
        {
            throw new Exception("Invalid Game Year specified.");
        }

        var league = new League
        {
            Name = name,
            GameYearId = gameYearId,
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
        // This implementation would typically require a new repository method like GetLeaguesByUserIdAsync
        // for performance. For now, this logic can reside in the service.
        throw new NotImplementedException("This requires a more complex query in the LeagueRepository.");
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

    private string GenerateEntryCode()
    {
        var chars = "ABCDEFGHJKLMNPQRSTUVWXYZ123456789";
        var random = new Random();
        var result = new char[6];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = chars[random.Next(chars.Length)];
        }
        return new string(result);
    }
}