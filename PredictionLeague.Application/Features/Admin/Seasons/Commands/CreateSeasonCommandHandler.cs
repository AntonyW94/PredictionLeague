using FluentValidation;
using FluentValidation.Results;
using MediatR;
using PredictionLeague.Application.Features.Admin.Teams.Queries;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Application.Services;
using PredictionLeague.Contracts.Admin.Seasons;
using PredictionLeague.Domain.Models;

namespace PredictionLeague.Application.Features.Admin.Seasons.Commands;

public class CreateSeasonCommandHandler : IRequestHandler<CreateSeasonCommand, SeasonDto>
{
    private readonly ISeasonRepository _seasonRepository;
    private readonly ILeagueRepository _leagueRepository;
    private readonly IFootballDataService _footballDataService;
    private readonly IMediator _mediator;

    public CreateSeasonCommandHandler(
        ISeasonRepository seasonRepository,
        ILeagueRepository leagueRepository,
        IFootballDataService footballDataService,
        IMediator mediator)
    {
        _seasonRepository = seasonRepository;
        _leagueRepository = leagueRepository;
        _footballDataService = footballDataService;
        _mediator = mediator;
    }

    public async Task<SeasonDto> Handle(CreateSeasonCommand request, CancellationToken cancellationToken)
    {
        await ValidateSeasonAgainstApiAsync(request, cancellationToken);

        var season = CreateSeasonEntity(request);
        var createdSeason = await _seasonRepository.CreateAsync(season, cancellationToken);

        if (createdSeason.ApiLeagueId.HasValue)
            await _mediator.Send(new SyncSeasonWithApiCommand(createdSeason.Id), cancellationToken);

        var publicLeague = CreatePublicLeagueEntity(request, createdSeason);
        await _leagueRepository.CreateAsync(publicLeague, cancellationToken);

        return MapToSeasonDto(createdSeason);
    }

    private async Task ValidateSeasonAgainstApiAsync(CreateSeasonCommand request, CancellationToken cancellationToken)
    {
        if (!request.ApiLeagueId.HasValue)
            return;

        var seasonYear = request.StartDate.Year;
        var validationFailures = new List<ValidationFailure>();

        try
        {
            var apiSeason = await _footballDataService.GetLeagueSeasonDetailsAsync(request.ApiLeagueId.Value, seasonYear, cancellationToken);
            if (apiSeason == null)
                throw new ValidationException($"The API returned no season data for League ID {request.ApiLeagueId.Value} and Year {seasonYear}. Please verify the details.");
            
            if (request.StartDate.Date != apiSeason.Start.Date)
                validationFailures.Add(new ValidationFailure(nameof(request.StartDate), $"The Start Date does not match the API. Expected: {apiSeason.Start:yyyy-MM-dd}, but you entered: {request.StartDate:yyyy-MM-dd}."));

            if (request.EndDate.Date != apiSeason.End.Date)
                validationFailures.Add(new ValidationFailure(nameof(request.EndDate), $"The End Date does not match the API. Expected: {apiSeason.End:yyyy-MM-dd}, but you entered: {request.EndDate:yyyy-MM-dd}."));
            
            var apiRoundNames = (await _footballDataService.GetRoundsForSeasonAsync(request.ApiLeagueId.Value, seasonYear, cancellationToken)).ToList();
            if (request.NumberOfRounds != apiRoundNames.Count)
                validationFailures.Add(new ValidationFailure(nameof(request.NumberOfRounds), $"The Number of Rounds does not match the API. Expected: {apiRoundNames.Count}, but you entered: {request.NumberOfRounds}."));
            
            var apiTeams = (await _footballDataService.GetTeamsForSeasonAsync(request.ApiLeagueId.Value, seasonYear, cancellationToken)).ToList();
            var localTeams = await _mediator.Send(new FetchAllTeamsQuery(), cancellationToken);

            var localTeamApiIds = localTeams
                .Where(t => t.ApiTeamId.HasValue)
                .Select(t => t.ApiTeamId.GetValueOrDefault())
                .ToHashSet();

            var missingTeams = apiTeams
                .Where(apiTeam => !localTeamApiIds.Contains(apiTeam.Team.Id))
                .Select(apiTeam => apiTeam.Team.Name)
                .ToList();

            if (missingTeams.Any())
                validationFailures.Add(new ValidationFailure(nameof(request.ApiLeagueId), $"The following teams from the API do not exist in the database: {string.Join(", ", missingTeams)}. Please add them before creating the season."));
        }
        catch (HttpRequestException ex)
        {
            throw new ValidationException($"Could not retrieve data from the football API. Please check your API key. Details: {ex.Message}");
        }

        if (validationFailures.Any())
            throw new ValidationException(validationFailures);
    }

    private static Season CreateSeasonEntity(CreateSeasonCommand request)
    {
        return Season.Create(
            request.Name,
            request.StartDate,
            request.EndDate,
            request.IsActive,
            request.NumberOfRounds,
            request.ApiLeagueId);
    }

    private static League CreatePublicLeagueEntity(CreateSeasonCommand request, Season createdSeason)
    {
        return League.CreateOfficialPublicLeague(
            createdSeason.Id,
            createdSeason.Name,
            0,
            request.CreatorId,
            createdSeason.StartDate.AddDays(-1),
            createdSeason
        );
    }

    private static SeasonDto MapToSeasonDto(Season createdSeason)
    {
        return new SeasonDto(
            createdSeason.Id,
            createdSeason.Name,
            createdSeason.StartDate,
            createdSeason.EndDate,
            createdSeason.IsActive,
            createdSeason.NumberOfRounds,
            0
        );
    }
}