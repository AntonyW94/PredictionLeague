using Ardalis.GuardClauses;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Application.Services;
using PredictionLeague.Domain.Common.Guards;

namespace PredictionLeague.Application.Features.Admin.Seasons.Commands;

public class UpdateSeasonCommandHandler : IRequestHandler<UpdateSeasonCommand>
{
    private readonly ISeasonRepository _seasonRepository;
    private readonly IFootballDataService _footballDataService;

    public UpdateSeasonCommandHandler(ISeasonRepository seasonRepository, IFootballDataService footballDataService)
    {
        _seasonRepository = seasonRepository;
        _footballDataService = footballDataService;
    }

    public async Task Handle(UpdateSeasonCommand request, CancellationToken cancellationToken)
    {
        var season = await _seasonRepository.GetByIdAsync(request.Id, cancellationToken);
        Guard.Against.EntityNotFound(request.Id, season, "Season");
       
        await ValidateSeasonAgainstApiAsync(request, cancellationToken);

        season.UpdateDetails(
            request.Name,
            request.StartDateUtc,
            request.EndDateUtc,
            request.IsActive,
            request.NumberOfRounds,
            request.ApiLeagueId
        );

        await _seasonRepository.UpdateAsync(season, cancellationToken);
    }

    private async Task ValidateSeasonAgainstApiAsync(UpdateSeasonCommand request, CancellationToken cancellationToken)
    {
        if (!request.ApiLeagueId.HasValue)
            return;
        
        var seasonYear = request.StartDateUtc.Year;
        var validationFailures = new List<ValidationFailure>();

        try
        {
            var apiSeason = await _footballDataService.GetLeagueSeasonDetailsAsync(request.ApiLeagueId.Value, seasonYear, cancellationToken);
            if (apiSeason == null)
                throw new ValidationException($"The API returned no season data for League ID {request.ApiLeagueId.Value} and Year {seasonYear}. Please verify the details.");
            
            if (request.StartDateUtc.Date != apiSeason.Start.Date)
                validationFailures.Add(new ValidationFailure(nameof(request.StartDateUtc), $"The Start Date does not match the API. Expected: {apiSeason.Start:yyyy-MM-dd}, but you entered: {request.StartDateUtc:yyyy-MM-dd}."));
            
            if (request.EndDateUtc.Date != apiSeason.End.Date)
                validationFailures.Add(new ValidationFailure(nameof(request.EndDateUtc), $"The End Date does not match the API. Expected: {apiSeason.End:yyyy-MM-dd}, but you entered: {request.EndDateUtc:yyyy-MM-dd}."));
            
            var apiRoundNames = (await _footballDataService.GetRoundsForSeasonAsync(request.ApiLeagueId.Value, seasonYear, cancellationToken)).ToList();
            if (request.NumberOfRounds != apiRoundNames.Count)
                validationFailures.Add(new ValidationFailure(nameof(request.NumberOfRounds), $"The Number of Rounds does not match the API. Expected: {apiRoundNames.Count}, but you entered: {request.NumberOfRounds}."));
        }
        catch (HttpRequestException ex)
        {
            throw new ValidationException($"Could not retrieve data from the football API. Please check your API key. Details: {ex.Message}");
        }

        if (validationFailures.Any())
            throw new ValidationException(validationFailures);
    }
}