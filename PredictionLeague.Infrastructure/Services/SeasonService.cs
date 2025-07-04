using PredictionLeague.Core.Models;
using PredictionLeague.Core.Repositories;
using PredictionLeague.Core.Services;
using PredictionLeague.Shared.Admin.Seasons;

namespace PredictionLeague.Infrastructure.Services;

public class SeasonService : ISeasonService
{
    private readonly ISeasonRepository _seasonRepository;
    private readonly IRoundRepository _roundRepository;

    public SeasonService(ISeasonRepository seasonRepository, IRoundRepository roundRepository)
    {
        _seasonRepository = seasonRepository;
        _roundRepository = roundRepository;
    }

    public async Task<IEnumerable<SeasonDto>> GetAllAsync()
    {
        var seasons = await _seasonRepository.GetAllAsync();
        var seasonsToReturn = new List<SeasonDto>();

        foreach (var season in seasons)
        {
            var rounds = await _roundRepository.GetBySeasonIdAsync(season.Id);
            seasonsToReturn.Add(MapSeasonToDto(season, rounds));
        }

        return seasonsToReturn;
    }

    public async Task<SeasonDto?> GetByIdAsync(int id)
    {
        var season = await _seasonRepository.GetByIdAsync(id);
        if (season == null)
            return null;

        var rounds = await _roundRepository.GetBySeasonIdAsync(id);
        return MapSeasonToDto(season, rounds);
    }

    private static SeasonDto MapSeasonToDto(Season season, IEnumerable<Round> rounds)
    {
        return new SeasonDto
        {
            Id = season.Id,
            Name = season.Name,
            StartDate = season.StartDate,
            EndDate = season.EndDate,
            IsActive = season.IsActive,
            RoundCount = rounds.Count()
        };
    }
}