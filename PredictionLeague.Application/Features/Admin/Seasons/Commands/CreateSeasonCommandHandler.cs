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

    public CreateSeasonCommandHandler(ISeasonRepository seasonRepository, ILeagueRepository leagueRepository, IFootballDataService footballDataService, IMediator mediator)
    {
        _seasonRepository = seasonRepository;
        _leagueRepository = leagueRepository;
        _footballDataService = footballDataService;
        _mediator = mediator;
    }

    public async Task<SeasonDto> Handle(CreateSeasonCommand request, CancellationToken cancellationToken)
    {
        await ValidateTeamsFromApiAsync(request, cancellationToken);

        var season = CreateSeasonEntity(request);
        var createdSeason = await _seasonRepository.CreateAsync(season, cancellationToken);

        var publicLeague = CreatePublicLeagueEntity(request, createdSeason);
        await _leagueRepository.CreateAsync(publicLeague, cancellationToken);

        return MapToSeasonDto(createdSeason);
    }

    private async Task ValidateTeamsFromApiAsync(CreateSeasonCommand request, CancellationToken cancellationToken)
    {
        if (!request.ApiLeagueId.HasValue)
            return;
        
        List<FootballApi.DTOs.TeamResponse> apiTeams;
   
        try
        {
            apiTeams = (await _footballDataService.GetTeamsForSeasonAsync(
                request.ApiLeagueId.Value,
                request.StartDate.Year,
                cancellationToken)).ToList();
        }
        catch (HttpRequestException ex)
        {
            throw new ValidationException($"Could not retrieve data from the football API. Please check your API key and network connection. Details: {ex.Message}");
        }

        if (!apiTeams.Any())
            throw new ValidationException($"The API returned no teams for the provided League ID ({request.ApiLeagueId.Value}) and Season Year ({request.StartDate.Year}). Please verify the details are correct.");

        var localTeams = await _mediator.Send(new FetchAllTeamsQuery(), cancellationToken);

        var localTeamApiIds = localTeams
            .Where(t => t.ApiTeamId.HasValue)
            .Select(t => t.ApiTeamId.GetValueOrDefault())
            .ToHashSet();

        var missingTeams = apiTeams
            .Where(apiTeam => !localTeamApiIds.Contains(apiTeam.Team.Id))
            .Select(apiTeam => apiTeam.Team.Name)
            .ToList();

        if (!missingTeams.Any())
            return;

        var missingTeamsString = string.Join(", ", missingTeams);
        var errorMessage = $"Cannot create season. The following teams from the API do not exist in the database: {missingTeamsString}. Please add them and set their API ID before creating the season.";

        var validationFailure = new ValidationFailure(propertyName: "ApiLeagueId", errorMessage: errorMessage);
        throw new ValidationException(new[] { validationFailure });
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