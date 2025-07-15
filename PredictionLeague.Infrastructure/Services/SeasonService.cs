using PredictionLeague.Application.Repositories;
using PredictionLeague.Application.Services;
using PredictionLeague.Contracts.Admin.Seasons;
using PredictionLeague.Domain.Models;

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

    #region Create

    public async Task CreateAsync(CreateSeasonRequest request)
    {
        var season = new Season { Name = request.Name, StartDate = request.StartDate, EndDate = request.EndDate, IsActive = true };
        await _seasonRepository.AddAsync(season);
    }

    #endregion

    #region Read

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

    #endregion

    #region Update

    public async Task UpdateAsync(int id, UpdateSeasonRequest request)
    {
        var season = await _seasonRepository.GetByIdAsync(id) ?? throw new KeyNotFoundException("Season not found.");

        season.Name = request.Name;
        season.StartDate = request.StartDate;
        season.EndDate = request.EndDate;
        season.IsActive = request.IsActive;

        await _seasonRepository.UpdateAsync(season);
    }

    #endregion
}