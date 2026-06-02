using Ardalis.GuardClauses;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.Extensions.Logging;
using ThePredictions.Application.Features.Admin.Teams.Queries;
using ThePredictions.Application.Repositories;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Admin.Seasons;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Common.Guards;
using ThePredictions.Domain.Models;

namespace ThePredictions.Application.Features.Admin.Seasons.Commands;

public class CreateSeasonCommandHandler(
    ISeasonRepository seasonRepository,
    ICompetitionRepository competitionRepository,
    ILeagueRepository leagueRepository,
    IRoundRepository roundRepository,
    ITournamentRoundMappingRepository tournamentRoundMappingRepository,
    IFootballDataService footballDataService,
    IMediator mediator,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    ILogger<CreateSeasonCommandHandler> logger) : IRequestHandler<CreateSeasonCommand, SeasonDto>
{
    public async Task<SeasonDto> Handle(CreateSeasonCommand request, CancellationToken cancellationToken)
    {
        currentUserService.EnsureAdministrator();

        var competition = await competitionRepository.GetByIdAsync(request.CompetitionId, cancellationToken);
        Guard.Against.EntityNotFound(request.CompetitionId, competition, "Competition");

        await ValidateSeasonAgainstApiAsync(request, competition, cancellationToken);

        var season = CreateSeasonEntity(request);
        var createdSeason = await seasonRepository.CreateAsync(season, cancellationToken);

        if (competition.IsTournament && request.TournamentRoundMappings.Any())
        {
            await SaveTournamentMappingsAndCreatePlaceholderRoundsAsync(createdSeason, request.TournamentRoundMappings, cancellationToken);
        }

        if (competition.ApiLeagueId.HasValue)
            await mediator.Send(new SyncSeasonWithApiCommand(createdSeason.Id), cancellationToken);

        var publicLeague = CreatePublicLeagueEntity(request, createdSeason);
        await leagueRepository.CreateAsync(publicLeague, cancellationToken);

        return MapToSeasonDto(createdSeason, competition);
    }

    private async Task ValidateSeasonAgainstApiAsync(CreateSeasonCommand request, Competition competition, CancellationToken cancellationToken)
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

            var apiTeams = (await footballDataService.GetTeamsForSeasonAsync(competition.ApiLeagueId.Value, seasonYear, cancellationToken)).ToList();
            var localTeams = await mediator.Send(new FetchAllTeamsQuery(), cancellationToken);

            var localTeamApiIds = localTeams
                .Where(t => t.ApiTeamId.HasValue)
                .Select(t => t.ApiTeamId.GetValueOrDefault())
                .ToHashSet();

            var missingTeams = apiTeams
                .Where(apiTeam => !localTeamApiIds.Contains(apiTeam.Team.Id))
                .Select(apiTeam => apiTeam.Team.Name)
                .ToList();

            if (missingTeams.Any())
                validationFailures.Add(new ValidationFailure(nameof(competition.ApiLeagueId), $"The following teams from the API do not exist in the database: {string.Join(", ", missingTeams)}. Please add them before creating the season."));
        }
        catch (HttpRequestException ex)
        {
            throw new ValidationException($"Could not retrieve data from the football API. Please check your API key. Details: {ex.Message}");
        }

        if (validationFailures.Any())
            throw new ValidationException(validationFailures);
    }

    private async Task SaveTournamentMappingsAndCreatePlaceholderRoundsAsync(
        Season season,
        List<TournamentRoundMappingDto> mappingDtos,
        CancellationToken cancellationToken)
    {
        var mappings = mappingDtos.Select(dto =>
            TournamentRoundMapping.Create(
                season.Id,
                dto.RoundNumber,
                dto.DisplayName,
                string.Join("|", dto.Stages),
                dto.ExpectedMatchCount)).ToList();

        await tournamentRoundMappingRepository.ReplaceAllForSeasonAsync(season.Id, mappings, cancellationToken);

        var globalMatchNumber = 1;

        foreach (var mapping in mappings)
        {
            var stages = mapping.GetStageList();
            var primaryStageDisplayName = stages.Count > 0
                ? TournamentRoundNameParser.GetDefaultDisplayName(stages[0])
                : null;

            var round = Round.Create(
                season.Id,
                mapping.RoundNumber,
                mapping.DisplayName,
                season.StartDateUtc,
                season.StartDateUtc.AddMinutes(-30),
                apiRoundName: primaryStageDisplayName);

            var createdRound = await roundRepository.CreateAsync(round, cancellationToken);

            var localMatchNumber = 1;
            for (var i = 0; i < mapping.ExpectedMatchCount; i++)
            {
                var stage = stages.Count == 1
                    ? stages[0]
                    : GetStageForMatchIndex(stages, i, mapping.ExpectedMatchCount);

                var placeholderName = TournamentRoundNameParser.GetPlaceholderMatchName(stage, localMatchNumber);
                var apiRoundNameForStage = TournamentRoundNameParser.GetDefaultDisplayName(stage);

                createdRound.AddPlaceholderMatch(placeholderName, placeholderName, apiRoundNameForStage, globalMatchNumber);
                localMatchNumber++;
                globalMatchNumber++;
            }

            await roundRepository.UpdateAsync(createdRound, cancellationToken);
            logger.LogInformation("Round (ID: {RoundId}) created with {MatchCount} placeholder matches for tournament Season (ID: {SeasonId})", createdRound.Id, createdRound.Matches.Count, season.Id);
        }
    }

    private static TournamentStage GetStageForMatchIndex(List<TournamentStage> stages, int matchIndex, int totalMatches)
    {
        // For combined knockout rounds, distribute matches across stages
        // using known knockout stage sizes (SF=2, ThirdPlace=1, Final=1)
        var cumulative = 0;
        foreach (var stage in stages)
        {
            var stageSize = stage switch
            {
                TournamentStage.SemiFinals => 2,
                TournamentStage.ThirdPlace => 1,
                TournamentStage.Final => 1,
                TournamentStage.QuarterFinals => 4,
                TournamentStage.RoundOf16 => 8,
                TournamentStage.RoundOf32 => 16,
                _ => totalMatches / stages.Count
            };

            cumulative += stageSize;
            if (matchIndex < cumulative)
                return stage;
        }

        return stages[^1];
    }

    // A blank or non-positive price means "free" - store NULL so RequiresPayment stays false.
    private static decimal? NormalisePrice(decimal? price)
        => price is > 0 ? price : null;

    private static Season CreateSeasonEntity(CreateSeasonCommand request)
    {
        return Season.Create(
            request.Name,
            request.StartDateUtc,
            request.EndDateUtc,
            request.IsActive,
            request.NumberOfRounds,
            request.CompetitionId,
            passStandardPrice: NormalisePrice(request.PassStandardPrice),
            passPremiumPrice: null);
    }

    private League CreatePublicLeagueEntity(CreateSeasonCommand request, Season createdSeason)
    {
        return League.CreateOfficialPublicLeague(
            createdSeason.Id,
            createdSeason.Name,
            0,
            request.CreatorId,
            createdSeason.StartDateUtc.AddDays(-1),
            createdSeason,
            dateTimeProvider
        );
    }

    private static SeasonDto MapToSeasonDto(Season createdSeason, Competition competition)
    {
        return new SeasonDto(
            createdSeason.Id,
            createdSeason.Name,
            createdSeason.StartDateUtc,
            createdSeason.EndDateUtc,
            createdSeason.IsActive,
            createdSeason.NumberOfRounds,
            competition.Id,
            competition.Name,
            (int)competition.Type,
            competition.ApiLeagueId,
            0, 0, 0, 0, 0, 0,
            createdSeason.PassStandardPrice,
            createdSeason.PassPremiumPrice
        );
    }
}
