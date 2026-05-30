using Ardalis.GuardClauses;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using ThePredictions.Application.Repositories;
using ThePredictions.Application.Services;
using ThePredictions.Domain.Common.Guards;
using ThePredictions.Domain.Models;

namespace ThePredictions.Application.Features.Admin.Seasons.Commands;

public class UpdateSeasonCommandHandler(
    ISeasonRepository seasonRepository,
    ICompetitionRepository competitionRepository,
    ITournamentRoundMappingRepository tournamentRoundMappingRepository,
    IFootballDataService footballDataService,
    ICurrentUserService currentUserService) : IRequestHandler<UpdateSeasonCommand>
{
    public async Task Handle(UpdateSeasonCommand request, CancellationToken cancellationToken)
    {
        currentUserService.EnsureAdministrator();

        var season = await seasonRepository.GetByIdAsync(request.Id, cancellationToken);
        Guard.Against.EntityNotFound(request.Id, season, "Season");

        var competition = await competitionRepository.GetByIdAsync(request.CompetitionId, cancellationToken);
        Guard.Against.EntityNotFound(request.CompetitionId, competition, "Competition");

        await ValidateSeasonAgainstApiAsync(request, competition, cancellationToken);

        season.UpdateDetails(
            request.Name,
            request.StartDateUtc,
            request.EndDateUtc,
            request.IsActive,
            request.NumberOfRounds,
            request.CompetitionId
        );

        await seasonRepository.UpdateAsync(season, cancellationToken);

        if (competition.IsTournament && request.TournamentRoundMappings.Any())
        {
            var mappings = request.TournamentRoundMappings.Select(dto =>
                TournamentRoundMapping.Create(
                    request.Id,
                    dto.RoundNumber,
                    dto.DisplayName,
                    string.Join("|", dto.Stages),
                    dto.ExpectedMatchCount)).ToList();

            await tournamentRoundMappingRepository.ReplaceAllForSeasonAsync(request.Id, mappings, cancellationToken);
        }
    }

    private async Task ValidateSeasonAgainstApiAsync(UpdateSeasonCommand request, Competition competition, CancellationToken cancellationToken)
    {
        if (!competition.ApiLeagueId.HasValue)
            return;

        var seasonYear = request.StartDateUtc.Year;
        var validationFailures = new List<ValidationFailure>();

        try
        {
            var apiSeason = await footballDataService.GetLeagueSeasonDetailsAsync(competition.ApiLeagueId.Value, seasonYear, cancellationToken);
            if (apiSeason == null)
                throw new ValidationException($"The API returned no season data for League ID {competition.ApiLeagueId.Value} and Year {seasonYear}. Please verify the details.");

            // Skip date and round count validation for tournaments — the API may not have
            // accurate end dates or all round names for future knockout stages
            if (!competition.IsTournament)
            {
                if (request.StartDateUtc.Date != apiSeason.Start.Date)
                    validationFailures.Add(new ValidationFailure(nameof(request.StartDateUtc), $"The Start Date does not match the API. Expected: {apiSeason.Start:yyyy-MM-dd}, but you entered: {request.StartDateUtc:yyyy-MM-dd}."));

                if (request.EndDateUtc.Date != apiSeason.End.Date)
                    validationFailures.Add(new ValidationFailure(nameof(request.EndDateUtc), $"The End Date does not match the API. Expected: {apiSeason.End:yyyy-MM-dd}, but you entered: {request.EndDateUtc:yyyy-MM-dd}."));

                var apiRoundNames = (await footballDataService.GetRoundsForSeasonAsync(competition.ApiLeagueId.Value, seasonYear, cancellationToken)).ToList();
                if (request.NumberOfRounds != apiRoundNames.Count)
                    validationFailures.Add(new ValidationFailure(nameof(request.NumberOfRounds), $"The Number of Rounds does not match the API. Expected: {apiRoundNames.Count}, but you entered: {request.NumberOfRounds}."));
            }
        }
        catch (HttpRequestException ex)
        {
            throw new ValidationException($"Could not retrieve data from the football API. Please check your API key. Details: {ex.Message}");
        }

        if (validationFailures.Any())
            throw new ValidationException(validationFailures);
    }
}
